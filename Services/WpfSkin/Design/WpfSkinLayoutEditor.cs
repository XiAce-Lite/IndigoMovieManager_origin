using System.Collections.ObjectModel;
using System.Text.Json;

namespace IndigoMovieManager.Services.WpfSkin.Design
{
    internal enum WpfSkinNodeKind
    {
        Text,
        Thumbnail,
        Tags,
        Stack,
        Grid,
    }

    internal static class WpfSkinLayoutEditor
    {
        public static readonly IReadOnlyList<string> FieldOptions =
        [
            "",
            "title",
            "body",
            "metatitle",
            "artist",
            "genre",
            "length",
            "size",
            "score",
            "viewcount",
            "path",
            "filedate",
            "registdate",
            "lastdate",
            "container",
            "video",
            "audio",
            "ext",
            "drive",
            "dir",
            "comment1",
            "comment2",
            "comment3",
        ];

        public static WpfSkinNode CreateNode(WpfSkinNodeKind kind)
        {
            return kind switch
            {
                WpfSkinNodeKind.Text => new WpfSkinNode
                {
                    Type = "text",
                    Field = "title",
                    Style = "title",
                    Wrap = true,
                },
                WpfSkinNodeKind.Thumbnail => new WpfSkinNode
                {
                    Type = "thumbnail",
                    Source = "local",
                },
                WpfSkinNodeKind.Tags => new WpfSkinNode
                {
                    Type = "tags",
                },
                WpfSkinNodeKind.Stack => new WpfSkinNode
                {
                    Panel = "stack",
                    Stack = "vertical",
                    Children = [],
                },
                WpfSkinNodeKind.Grid => new WpfSkinNode
                {
                    Panel = "grid",
                    Rows = ["auto"],
                    Columns = ["*"],
                    Children = [],
                },
                _ => new WpfSkinNode(),
            };
        }

        public static bool CanContainChildren(WpfSkinNode node)
        {
            if (node == null)
            {
                return false;
            }

            // ResolvePanel() は未指定時 stack になるため、明示 Panel か実子で判定する。
            if (string.Equals(node.Panel, "stack", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.Panel, "grid", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return node.Children is { Count: > 0 };
        }

        public static WpfSkinNode CreateNodeFromField(string fieldId, bool isListSkin = false)
        {
            WpfSkinFieldDescriptor desc = WpfSkinFieldCatalog.GetRequired(fieldId);
            string defaultStyle = WpfSkinFieldCatalog.GetDefaultStyleKey(desc.Id);
            WpfSkinNode node = desc.Kind switch
            {
                WpfSkinFieldKind.Thumbnail => CreateThumbnailNodeFromFieldId(desc.Id),
                WpfSkinFieldKind.Tags => new WpfSkinNode
                {
                    Type = "tags",
                    Wrap = true,
                },
                WpfSkinFieldKind.Path => new WpfSkinNode
                {
                    Type = "text",
                    Field = desc.Id,
                    Wrap = true,
                    Link = true,
                    Style = defaultStyle,
                },
                _ => new WpfSkinNode
                {
                    Type = "text",
                    Field = desc.Id,
                    Wrap = true,
                    Style = defaultStyle,
                    Format = string.Equals(desc.Id, "size", StringComparison.OrdinalIgnoreCase) ? "filesize" : "",
                },
            };

            if (desc.Kind is WpfSkinFieldKind.Text or WpfSkinFieldKind.Path)
            {
                if (isListSkin)
                {
                    node.Header = desc.DisplayName;
                }
                else
                {
                    node.Label = desc.DisplayName + ":";
                }
            }

            return node;
        }

        private static WpfSkinNode CreateThumbnailNodeFromFieldId(string fieldId)
        {
            if (string.Equals(fieldId, WpfSkinFieldCatalog.ThumbnailJacketId, StringComparison.OrdinalIgnoreCase))
            {
                return new WpfSkinNode
                {
                    Type = "thumbnail",
                    Source = "comment1",
                    Width = WpfSkinThumbnailSources.JacketInfoFallbackWidth,
                    Height = WpfSkinThumbnailSources.JacketInfoFallbackHeight,
                    VAlign = "top",
                    HAlign = "left",
                };
            }

            // thumbnail:local（および旧 thumbnail）
            return new WpfSkinNode
            {
                Type = "thumbnail",
                Source = "local",
                VAlign = "top",
                HAlign = "left",
            };
        }

        public static bool IsFieldUsed(WpfSkinNode root, string fieldId)
        {
            if (string.IsNullOrWhiteSpace(fieldId))
            {
                return false;
            }

            return WpfSkinFieldCatalog.CollectUsedFieldIds(root)
                .Contains(fieldId.Trim());
        }

        /// <summary>
        /// layout 全体で一意制約を確認してから field を挿入する。
        /// </summary>
        public static bool TryInsertField(
            WpfSkinNode layoutRoot,
            WpfSkinNode parent,
            string fieldId,
            int index,
            out WpfSkinNode added,
            out string error,
            bool isListSkin = false)
        {
            added = null;
            error = null;
            if (layoutRoot == null || parent == null)
            {
                error = "追加先がありません。";
                return false;
            }

            if (!WpfSkinFieldCatalog.TryGet(fieldId, out _))
            {
                error = $"不明な項目です: {fieldId}";
                return false;
            }

            if (IsFieldUsed(layoutRoot, fieldId))
            {
                error = $"「{fieldId}」は既に配置されています。";
                return false;
            }

            added = CreateNodeFromField(fieldId, isListSkin);
            EnsureContainer(parent);
            int safeIndex = Math.Clamp(index, 0, parent.Children.Count);
            parent.Children.Insert(safeIndex, added);
            return true;
        }

        public static WpfSkinNode AddChild(WpfSkinNode parent, WpfSkinNodeKind kind)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            EnsureContainer(parent);
            WpfSkinNode child = CreateNode(kind);
            parent.Children.Add(child);
            return child;
        }

        public static WpfSkinNode InsertChild(WpfSkinNode parent, WpfSkinNodeKind kind, int index)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            EnsureContainer(parent);
            WpfSkinNode child = CreateNode(kind);
            int safeIndex = Math.Clamp(index, 0, parent.Children.Count);
            parent.Children.Insert(safeIndex, child);
            return child;
        }

        public static WpfSkinNode CloneNode(WpfSkinNode source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            string json = JsonSerializer.Serialize(source, options);
            return JsonSerializer.Deserialize<WpfSkinNode>(json, options)
                ?? throw new InvalidOperationException("Failed to clone WpfSkinNode.");
        }

        public static WpfSkinNode InsertClonedChild(WpfSkinNode parent, WpfSkinNode source, int index)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            EnsureContainer(parent);
            WpfSkinNode child = CloneNode(source);
            int safeIndex = Math.Clamp(index, 0, parent.Children.Count);
            parent.Children.Insert(safeIndex, child);
            return child;
        }

        public static bool MoveNodeToParent(WpfSkinNode sourceParent, WpfSkinNode node, WpfSkinNode targetParent, int insertIndex)
        {
            if (sourceParent?.Children == null || targetParent == null || node == null)
            {
                return false;
            }

            int sourceIndex = sourceParent.Children.IndexOf(node);
            if (sourceIndex < 0)
            {
                return false;
            }

            EnsureContainer(targetParent);

            sourceParent.Children.RemoveAt(sourceIndex);
            int safeIndex = Math.Clamp(insertIndex, 0, targetParent.Children.Count);
            targetParent.Children.Insert(safeIndex, node);
            return true;
        }

        public static bool RemoveNode(WpfSkinNode parent, WpfSkinNode node)
        {
            if (parent?.Children == null || node == null)
            {
                return false;
            }

            return parent.Children.Remove(node);
        }

        public static bool MoveNode(IList<WpfSkinNode> siblings, WpfSkinNode node, int delta)
        {
            if (siblings == null || node == null || delta == 0)
            {
                return false;
            }

            int index = siblings.IndexOf(node);
            if (index < 0)
            {
                return false;
            }

            int target = index + delta;
            if (target < 0 || target >= siblings.Count)
            {
                return false;
            }

            siblings.RemoveAt(index);
            siblings.Insert(target, node);
            return true;
        }

        public static void AssignGridSlot(WpfSkinNode node, int row, int col)
        {
            if (node == null)
            {
                return;
            }

            node.Row = Math.Max(0, row);
            node.Col = Math.Max(0, col);
            if (node.RowSpan < 1)
            {
                node.RowSpan = 1;
            }

            if (node.ColSpan < 1)
            {
                node.ColSpan = 1;
            }
        }

        public static bool TryAddStyle(WpfSkinDefinition definition, string key, out string errorMessage) =>
            TryAddStyle(definition, key, initial: null, out errorMessage);

        public static void EnsureStyleExists(WpfSkinDefinition definition, string key)
        {
            if (definition == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            definition.Styles ??= new Dictionary<string, WpfSkinStyle>(StringComparer.OrdinalIgnoreCase);
            if (definition.Styles.Keys.Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            definition.Styles[key.Trim()] = CreateStylePreset(key);
        }

        public static bool TryAddStyle(
            WpfSkinDefinition definition,
            string key,
            WpfSkinStyle initial,
            out string errorMessage)
        {
            errorMessage = null;
            if (!TryNormalizeStyleKey(key, out string normalized, out errorMessage))
            {
                return false;
            }

            definition.Styles ??= new Dictionary<string, WpfSkinStyle>(StringComparer.OrdinalIgnoreCase);
            if (definition.Styles.Keys.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = $"style \"{normalized}\" は既に存在します。";
                return false;
            }

            definition.Styles[normalized] = CloneStyle(initial) ?? new WpfSkinStyle();
            return true;
        }

        /// <summary>よく使う style プリセット。</summary>
        public static WpfSkinStyle CreateStylePreset(string presetId) =>
            (presetId ?? "").Trim().ToLowerInvariant() switch
            {
                "title" => new WpfSkinStyle
                {
                    FontSize = 14,
                    Bold = true,
                    Foreground = "#222222",
                },
                "meta" => new WpfSkinStyle
                {
                    FontSize = 12,
                    Foreground = "#666666",
                },
                "path" => new WpfSkinStyle
                {
                    FontSize = 11,
                    Foreground = "#666666",
                    Wrap = true,
                },
                _ => new WpfSkinStyle(),
            };

        private static WpfSkinStyle CloneStyle(WpfSkinStyle source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfSkinStyle
            {
                FontSize = source.FontSize,
                FontFamily = source.FontFamily ?? "",
                Bold = source.Bold,
                Italic = source.Italic,
                Foreground = source.Foreground ?? "",
                Background = source.Background ?? "",
                Align = source.Align ?? "",
                Wrap = source.Wrap,
            };
        }

        public static bool TryRenameStyle(WpfSkinDefinition definition, string oldKey, string newKey, out string errorMessage)
        {
            errorMessage = null;
            if (definition?.Styles == null || string.IsNullOrWhiteSpace(oldKey) || !definition.Styles.ContainsKey(oldKey))
            {
                errorMessage = "変更対象の style が見つかりません。";
                return false;
            }

            if (!TryNormalizeStyleKey(newKey, out string normalized, out errorMessage))
            {
                return false;
            }

            if (!string.Equals(oldKey, normalized, StringComparison.OrdinalIgnoreCase)
                && definition.Styles.Keys.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = $"style \"{normalized}\" は既に存在します。";
                return false;
            }

            WpfSkinStyle style = definition.Styles[oldKey];
            definition.Styles.Remove(oldKey);
            definition.Styles[normalized] = style;
            RewriteStyleReferences(definition.Card?.Layout, oldKey, normalized);
            return true;
        }

        public static bool DeleteStyle(WpfSkinDefinition definition, string key)
        {
            if (definition?.Styles == null || string.IsNullOrWhiteSpace(key) || !definition.Styles.Remove(key))
            {
                return false;
            }

            RewriteStyleReferences(definition.Card?.Layout, key, string.Empty);
            return true;
        }

        public static string FormatSpacing(WpfSkinSpacing spacing)
        {
            if (spacing == null || spacing.IsEmpty)
            {
                return "";
            }

            if (spacing.Left == spacing.Top && spacing.Top == spacing.Right && spacing.Right == spacing.Bottom)
            {
                return spacing.Left.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return FormattableString.Invariant($"{spacing.Left},{spacing.Top},{spacing.Right},{spacing.Bottom}");
        }

        private static void EnsureContainer(WpfSkinNode node)
        {
            node.Children ??= [];
            string panel = node.ResolvePanel();
            if (!string.Equals(panel, "stack", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(panel, "grid", StringComparison.OrdinalIgnoreCase))
            {
                node.Panel = "stack";
            }

            if (string.Equals(node.ResolvePanel(), "grid", StringComparison.OrdinalIgnoreCase))
            {
                node.Rows ??= ["auto"];
                node.Columns ??= ["*"];
            }
        }

        private static bool TryNormalizeStyleKey(string key, out string normalized, out string errorMessage)
        {
            normalized = key?.Trim() ?? "";
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                errorMessage = "style キーを入力してください。";
                return false;
            }

            return true;
        }

        private static void RewriteStyleReferences(WpfSkinNode node, string oldKey, string newKey)
        {
            if (node == null)
            {
                return;
            }

            if (string.Equals(node.Style, oldKey, StringComparison.OrdinalIgnoreCase))
            {
                node.Style = newKey ?? "";
            }

            foreach (WpfSkinNode child in node.Children ?? [])
            {
                RewriteStyleReferences(child, oldKey, newKey);
            }
        }
    }

    internal sealed class WpfSkinLayoutTreeNode : System.ComponentModel.INotifyPropertyChanged
    {
        public WpfSkinLayoutTreeNode(WpfSkinNode model, WpfSkinLayoutTreeNode parent = null)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Parent = parent;
            Children = new ObservableCollection<WpfSkinLayoutTreeNode>(
                (model.Children ?? [])
                    .Select(child => new WpfSkinLayoutTreeNode(child, this)));
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public WpfSkinNode Model { get; }

        public WpfSkinLayoutTreeNode Parent { get; private set; }

        public ObservableCollection<WpfSkinLayoutTreeNode> Children { get; }

        public bool IsRoot => Parent == null;

        public bool IsContainer => Model.IsContainer;

        public string DisplayName
        {
            get
            {
                if (Model.IsContainer)
                {
                    string panel = Model.ResolvePanel();
                    int count = Model.Children?.Count ?? 0;
                    return $"{panel} ({count})";
                }

                string leaf = string.IsNullOrWhiteSpace(Model.Type) ? "text" : Model.Type.Trim();
                if (string.Equals(leaf, "thumbnail", StringComparison.OrdinalIgnoreCase))
                {
                    string src = Model.Source?.Trim().ToLowerInvariant() ?? "";
                    return src switch
                    {
                        "comment1" => "thumbnail: jacket",
                        "local" => "thumbnail: local",
                        _ => "thumbnail",
                    };
                }

                string detail = !string.IsNullOrWhiteSpace(Model.Field)
                    ? Model.Field.Trim()
                    : (!string.IsNullOrWhiteSpace(Model.Label) ? Model.Label.Trim() : "");
                return string.IsNullOrWhiteSpace(detail) ? leaf : $"{leaf}: {detail}";
            }
        }

        public void NotifyDisplayNameChanged() =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(DisplayName)));

        public void Reparent(WpfSkinLayoutTreeNode parent)
        {
            Parent = parent;
        }

        public static ObservableCollection<WpfSkinLayoutTreeNode> BuildRoot(WpfSkinNode root) =>
            [new WpfSkinLayoutTreeNode(root)];

        public WpfSkinLayoutTreeNode FindByModel(WpfSkinNode node)
        {
            if (ReferenceEquals(Model, node))
            {
                return this;
            }

            foreach (WpfSkinLayoutTreeNode child in Children)
            {
                WpfSkinLayoutTreeNode found = child.FindByModel(node);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
