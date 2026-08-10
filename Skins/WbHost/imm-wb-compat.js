// WhiteBrowser 互換ブリッジ（WebView2 注入）。
// window.external.execCmd を提供し、C# ホストと wb スキンを接続する。
(function () {
    const state = {
        config: {},
        items: [],
        itemById: {},
        selectedIds: new Set(),
        focusedId: 0,
        order: [],
    };

    const DOM_BATCH = 48;
    let appendScheduled = false;
    let pendingScrollTop = null;

    function post(message) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(message);
        }
    }

    function parseCmd(cmd) {
        const q = cmd.indexOf("?");
        let func = q >= 0 ? cmd.slice(0, q) : cmd;
        if (func.endsWith("?")) {
            func = func.slice(0, -1);
        }
        const params = {};
        if (q >= 0) {
            cmd.slice(q + 1).split("&").forEach(pair => {
                if (!pair) return;
                const eq = pair.indexOf("=");
                if (eq < 0) {
                    params[decodeURIComponent(pair)] = "";
                    return;
                }
                const key = decodeURIComponent(pair.slice(0, eq));
                const val = decodeURIComponent(pair.slice(eq + 1).replace(/\+/g, " "));
                params[key] = val;
            });
        }
        return { func, params };
    }

    function parseConfig() {
        const base = {
            skinVersion: 1,
            thumbWidth: 160,
            thumbHeight: 120,
            thumbColumn: 1,
            thumbRow: 1,
            multiSelect: 1,
            seamlessScroll: 0,
            scrollId: "view",
        };
        const el = document.getElementById("config");
        const text = el ? el.textContent : "";
        text.split(";").forEach(part => {
            const idx = part.indexOf(":");
            if (idx < 0) return;
            const key = part.slice(0, idx).trim().toLowerCase();
            const value = part.slice(idx + 1).trim();
            switch (key) {
                case "skin-version": base.skinVersion = parseInt(value, 10) || 1; break;
                case "thum-width": base.thumbWidth = parseInt(value, 10) || base.thumbWidth; break;
                case "thum-height": base.thumbHeight = parseInt(value, 10) || base.thumbHeight; break;
                case "thum-column": base.thumbColumn = parseInt(value, 10) || base.thumbColumn; break;
                case "thum-row": base.thumbRow = parseInt(value, 10) || base.thumbRow; break;
                case "multi-select": base.multiSelect = parseInt(value, 10) || 0; break;
                case "seamless-scroll": base.seamlessScroll = parseInt(value, 10) || 0; break;
                case "scroll-id": base.scrollId = value || "view"; break;
            }
        });
        return base;
    }

    function scrollContainer() {
        const id = state.config.scrollId || "view";
        return document.getElementById(id)
            || document.getElementById("scroll")
            || document.scrollingElement
            || document.body;
    }

    function thumId(movieId) {
        return "thum" + movieId;
    }

    function imgId(movieId) {
        return "img" + movieId;
    }

    function parseThumId(elem) {
        if (!elem || !elem.id) return 0;
        const m = /^thum(\d+)$/.exec(elem.id);
        return m ? parseInt(m[1], 10) : 0;
    }

    function parseImgId(img) {
        if (!img || !img.id) return 0;
        const m = /^img(\d+)$/.exec(img.id);
        return m ? parseInt(m[1], 10) : 0;
    }

    function storeItems(items, reset) {
        if (reset) {
            state.items = items || [];
            state.itemById = {};
            state.order = [];
        } else if (items && items.length) {
            state.items = state.items.concat(items);
        }
        (items || []).forEach(mv => {
            state.itemById[mv.id] = mv;
            if (!state.order.includes(mv.id)) {
                state.order.push(mv.id);
            }
        });
    }

    function renderRange(items, reset) {
        if (!window.wb || typeof wb.onCreateThum !== "function") {
            return;
        }
        if (reset) {
            wb.onClearAll();
        }
        if (!items || !items.length) {
            return;
        }

        // Prototype の Insertion はアイテムごとに reflow するため、
        // 挿入中だけコンテナを display:none にして 1 バッチ 1 回の reflow に抑える。
        const view = document.getElementById("view");
        const prevDisplay = view ? view.style.display : "";
        if (view) {
            view.style.display = "none";
        }
        try {
            items.forEach(mv => {
                wb.onCreateThum(mv, 1);
            });
        } finally {
            if (view) {
                view.style.display = prevDisplay;
            }
        }
    }

    function scheduleAppend(items) {
        if (appendScheduled || !items || !items.length) return;
        appendScheduled = true;
        let index = 0;
        function step() {
            appendScheduled = false;
            if (!window.wb || index >= items.length) {
                restorePendingScroll();
                return;
            }
            const end = Math.min(index + DOM_BATCH, items.length);
            renderRange(items.slice(index, end), false);
            index = end;
            restorePendingScroll();
            if (index < items.length) {
                appendScheduled = true;
                requestAnimationFrame(step);
            }
        }
        requestAnimationFrame(step);
    }

    function restorePendingScroll() {
        if (pendingScrollTop == null) {
            return;
        }

        const sc = scrollContainer();
        if (!sc) {
            return;
        }

        sc.scrollTop = pendingScrollTop;
        ensureFocusedVisible();

        // バッチ描画で高さが足りない間は保持し、到達できたら解除
        const maxScroll = Math.max(0, sc.scrollHeight - sc.clientHeight);
        if (maxScroll >= pendingScrollTop - 1) {
            pendingScrollTop = null;
        }
    }

    function ensureFocusedVisible() {
        const id = state.focusedId;
        if (!id) {
            return;
        }

        const el = document.getElementById(thumId(id));
        if (el && typeof el.scrollIntoView === "function") {
            el.scrollIntoView({ block: "nearest", inline: "nearest" });
        }
    }

    function applyHostSelection(ids, focusedId) {
        state.selectedIds = new Set(ids || []);
        state.focusedId = focusedId || 0;
        if (!window.wb) return;
        state.order.forEach(id => {
            const sel = state.selectedIds.has(id) ? 1 : 0;
            wb.onSetSelect(id, sel);
            wb.onSetFocus(id, id === state.focusedId ? 1 : 0);
        });
    }

    function postSelection() {
        post({
            type: "select",
            ids: [...state.selectedIds],
            focusedId: state.focusedId || null,
        });
    }

    function selectMovie(id, event) {
        const multi = state.config.multiSelect === 1;
        let ids;
        if (multi && event && event.ctrlKey) {
            ids = new Set(state.selectedIds);
            if (ids.has(id)) ids.delete(id); else ids.add(id);
        } else if (multi && event && event.shiftKey && state.focusedId) {
            const start = state.order.indexOf(state.focusedId);
            const end = state.order.indexOf(id);
            if (start < 0 || end < 0) {
                ids = new Set([id]);
            } else {
                const lo = Math.min(start, end);
                const hi = Math.max(start, end);
                ids = new Set();
                for (let i = lo; i <= hi; i++) ids.add(state.order[i]);
            }
        } else {
            ids = new Set([id]);
        }
        state.selectedIds = ids;
        state.focusedId = id;
        applyHostSelection([...ids], id);
        postSelection();
    }

    function currentIndex() {
        if (!state.focusedId) return -1;
        return state.order.indexOf(state.focusedId);
    }

    function getColumns() {
        const cards = document.querySelectorAll("div.thum, div.thum_select, tr.thum, tr.thum_select");
        if (!cards.length) return 1;
        const top = cards[0].offsetTop;
        let cols = 0;
        for (const card of cards) {
            if (card.offsetTop === top) cols++;
            else break;
        }
        return Math.max(1, cols);
    }

    function selectByIndex(index) {
        if (!state.order.length) return;
        const clamped = Math.max(0, Math.min(state.order.length - 1, index));
        const id = state.order[clamped];
        selectMovie(id, null);
        const el = document.getElementById(thumId(id));
        if (el) el.scrollIntoView({ block: "nearest" });
    }

    function navigateKey(key, ctrl) {
        if (!state.order.length) return false;
        let idx = currentIndex();
        if (idx < 0) idx = 0;
        const cols = getColumns();
        const sc = scrollContainer();
        switch (key) {
            case "Home":
                if (ctrl) sc.scrollTop = 0;
                else selectByIndex(0);
                return true;
            case "End":
                if (ctrl) sc.scrollTop = sc.scrollHeight;
                else selectByIndex(state.order.length - 1);
                return true;
            case "ArrowLeft": selectByIndex(idx - 1); return true;
            case "ArrowRight": selectByIndex(idx + 1); return true;
            case "ArrowUp": selectByIndex(idx - cols); return true;
            case "ArrowDown": selectByIndex(idx + cols); return true;
            case "PageUp": sc.scrollTop -= sc.clientHeight; return true;
            case "PageDown": sc.scrollTop += sc.clientHeight; return true;
            default: return false;
        }
    }

    function applyScrollFix() {
        if (document.getElementById("imm-wb-scroll-fix")) {
            return;
        }

        const style = document.createElement("style");
        style.id = "imm-wb-scroll-fix";
        style.textContent = [
            "html, body {",
            "  width: 100%;",
            "  height: 100%;",
            "  margin: 0;",
            "  overflow: hidden;",
            "  box-sizing: border-box;",
            "}",
            "div#view {",
            "  box-sizing: border-box;",
            "  width: 100%;",
            "  height: 100%;",
            "  overflow-x: hidden;",
            "  overflow-y: auto;",
            "}",
        ].join("\n");
        (document.head || document.documentElement).appendChild(style);
    }

    function setupInteraction() {
        const view = document.getElementById("view");
        if (!view || view.__immWbBound) return;
        view.__immWbBound = true;

        view.addEventListener("click", event => {
            const tagLink = event.target.closest("a");
            if (tagLink && tagLink.getAttribute("href") && tagLink.getAttribute("href").indexOf("javascript:wb.find") >= 0) {
                return;
            }
            const thum = event.target.closest("div.thum, div.thum_select, tr.thum, tr.thum_select");
            if (!thum) return;
            const id = parseThumId(thum);
            if (!id) return;
            selectMovie(id, event);
        });

        view.addEventListener("dblclick", event => {
            const img = event.target.closest("img.img_thum, img.img_focus");
            if (!img) return;
            const id = parseImgId(img);
            if (!id) return;
            event.preventDefault();
            const rect = img.getBoundingClientRect();
            let suppress = 0;
            if (window.wb && typeof wb.onExec === "function") {
                suppress = wb.onExec(id, 0, 0);
            }
            if (suppress > 0) return;
            post({
                type: "play",
                id,
                clickX: event.clientX - rect.left,
                clickY: event.clientY - rect.top,
                imgWidth: rect.width,
                imgHeight: rect.height,
            });
        });

        document.addEventListener("keydown", event => {
            if (navigateKey(event.key, event.ctrlKey)) {
                event.preventDefault();
            }
        });
    }

    function onHostMessage(event) {
        const msg = event.data;
        if (!msg || !msg.type) return;

        if (msg.type === "wbRender") {
            const sc = scrollContainer();
            if (msg.reset && sc) {
                pendingScrollTop = sc.scrollTop;
            }

            const items = msg.items || [];
            if (msg.reset) {
                storeItems(items, true);
                if (items.length <= DOM_BATCH) {
                    renderRange(items, true);
                } else {
                    renderRange(items.slice(0, DOM_BATCH), true);
                    scheduleAppend(items.slice(DOM_BATCH));
                }
            } else {
                storeItems(items, false);
                renderRange(items, false);
            }
            if (msg.selectedIds) applyHostSelection(msg.selectedIds, msg.focusedId);
            requestAnimationFrame(() => restorePendingScroll());
        } else if (msg.type === "wbSelection") {
            applyHostSelection(msg.ids || [], msg.focusedId);
        } else if (msg.type === "wbUpdateThum") {
            if (window.wb && msg.id != null && msg.thum) {
                wb.onUpdateThum(imgId(msg.id), msg.thum);
            }
        } else if (msg.type === "keyNav") {
            navigateKey(msg.key, !!msg.ctrl);
        }
    }

    window.external = {
        execCmd: function (cmd) {
            const { func, params } = parseCmd(cmd || "");
            switch (func) {
                case "find":
                    post({ type: "searchTag", tag: params.key || "", ctrl: false });
                    return "";
                case "removeTag":
                    post({
                        type: "removeTag",
                        id: parseInt(params.mv, 10) || 0,
                        tag: params.tag || "",
                    });
                    return "";
                case "exec": {
                    const id = parseInt(params.mv, 10) || state.focusedId || 0;
                    const start = parseInt(params.start, 10) || 0;
                    if (window.wb && typeof wb.onExec === "function") {
                        const suppress = wb.onExec(id, parseInt(params.player, 10) || 0, start);
                        if (suppress > 0) return suppress;
                    }
                    post({ type: "play", id, start });
                    return 0;
                }
                case "focusThum": {
                    const id = parseInt(params.mv, 10) || 0;
                    state.focusedId = id;
                    if (window.wb) wb.onSetFocus(id, 1);
                    return "";
                }
                case "selectThum": {
                    const id = parseInt(params.mv, 10) || 0;
                    const sel = parseInt(params.sel, 10) ? 1 : 0;
                    if (sel) state.selectedIds.add(id); else state.selectedIds.delete(id);
                    if (window.wb) wb.onSetSelect(id, sel);
                    postSelection();
                    return "";
                }
                case "scrollTo": {
                    const id = parseInt(params.mv, 10) || state.focusedId || 0;
                    const el = document.getElementById(thumId(id));
                    if (el) el.scrollIntoView({ block: "nearest" });
                    return "";
                }
                case "scrollSetting":
                case "trace":
                    return "";
                case "getFocusThum":
                    return String(state.focusedId || 0);
                case "getSelectThums":
                    return JSON.stringify([...state.selectedIds]);
                case "getInfo": {
                    let id = parseInt(params.id, 10) || 0;
                    if (!id) id = state.focusedId;
                    const mv = state.itemById[id];
                    return mv ? JSON.stringify(mv) : "";
                }
                default:
                    return "";
            }
        },
    };

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.addEventListener("message", onHostMessage);
    }

    window.addEventListener("load", () => {
        applyScrollFix();
        state.config = parseConfig();
        setupInteraction();
        setTimeout(() => {
            if (window.wb && typeof wb.onSkinEnter === "function") {
                try { wb.onSkinEnter(); } catch (e) { /* ignore */ }
            }
            post({ type: "ready", config: state.config });
        }, 0);
    });
})();
