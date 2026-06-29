
/*--------------------------------------------------------------------------*/
/*  White Browser Library
 *  (c) 2007-  268@Gt
 *  http://www12.atwiki.jp/whitebrowser/
/*--------------------------------------------------------------------------*/

document.write('<script type="text/javascript" src="../tiny_segmenter.js" charset="UTF-8"></script>');

/**
   white browser super class
*/
var wbsuper = Class.create();

wbsuper.prototype = 
{
	app_version : '0.7.5.0',
	skin_version : '1',
	
	initialize : function() { },
	
	//-----------------------------------------------------------------------
	//**  onCreateThum
	/**   サムネイルを追加する必要があるときにコールされる
	*     seamless-scrollがonの時は必ずこの関数をオーバーライトしてサムネイルを作成してください
	*     @param	mv : ファイルの全情報
	*     @param	dir : 追加する方向、-1:先頭に、1:末尾に
	*///---------------------------------------------------------------------
	onCreateThum : function(mv, dir)
	{
		wb.trace("onCreateThumをオーバーライトしてください");
	},
	
	//-----------------------------------------------------------------------
	//**  onUpdate
	/**   検索結果が更新されたときにコールされる
	*     seamless-scrollがoffの時は必ずこの関数をオーバーライトしてサムネイルを作成してください
	*     @param	mvs : SELECT結果のファイル情報オブジェクト配列
	*     @param	param : updateによってこの関数が呼ばれた場合、
	*                       updateで渡された文字列が入る
	*///---------------------------------------------------------------------
	onUpdate : function(mvs, param)
	{
		wb.trace("onUpdateをオーバーライトしてください");
	},
	
	//-----------------------------------------------------------------------
	//**  onSkinEnter
	/**   スキン(エクステンション)が完全ロードされた後にコールされる
	*     必要な初期化処理を追加してください。
	*///---------------------------------------------------------------------
	onSkinEnter : function()
	{
		
	},
	
	//-----------------------------------------------------------------------
	//**  onSkinLeave
	/**   スキン(エクステンション)から離れようとしたときにコールされる
	*     必要な後処理を追加してください。
	*///---------------------------------------------------------------------
	onSkinLeave : function()
	{
		
	},
	
	//-----------------------------------------------------------------------
	//**  onClearAll
	/**   すべてのサムネイルを削除する必要があるときにコールされる
	*     $("view")以外のところにサムネイルを入れているときはオーバーライトしてください
	*///---------------------------------------------------------------------
	onClearAll : function()
	{
		var view = $("view");
		if(view == null) return;
		
		Element.update(view, "");
	},
	
	//-----------------------------------------------------------------------
	//**  onUpdateThum
	/**   サムネイルが更新され、再描画する必要があるときにコールされる
	*     IE7から、同じ値をsrcに設定しても再描画されなくなった
	*     一度srcをクリアしてから設定してください
	*///---------------------------------------------------------------------
	onUpdateThum : function(id, src)
	{
		var img = $(id);
		if(img == null) return;
		
		img.src = "";
		img.src = src;
	},
	
	//-----------------------------------------------------------------------
	//**  onRegistedFile
	/**   ファイルが登録された後にコールされる
	*     @param	id : 登録されたファイルのID
	*///---------------------------------------------------------------------
	onRegistedFile : function(id)
	{
		
	},
	
	//-----------------------------------------------------------------------
	//**  onRemoveFile
	/**   ファイルが登録から削除させる直前にコールされる
	*     @param	id : これから登録削除するファイルのID
	*///---------------------------------------------------------------------
	onRemoveFile : function(id)
	{
		
	},
	
	//-----------------------------------------------------------------------
	//**  onSetFocus
	/**   サムネイルのフォーカス状態が変わったときにコールされる
	*      @param	id : ファイルのID
	*      @param	isFocus : フォーカスを持つ(1)、持たない(0)
	*///---------------------------------------------------------------------
	onSetFocus : function(id, isFocus)
	{
		var img = $("img" + id);
		if(img == null) return;
		
		if(isFocus){
			img.className = "img_focus";
		}else{
			img.className = "img_thum";
		}
	},
	
	//-----------------------------------------------------------------------
	//**  onSetSelect
	/**   サムネイルの選択状態が変わったときにコールされる
	*     ただし、高速化のためサムネイルが表示範囲外のときコールされない場合がある
	*      @param	id : ファイルのID
	*      @param	isSelect : 選択状態(1)、非選択状態(0)
	*///---------------------------------------------------------------------
	onSetSelect : function(id, isSelect)
	{
		var thum = $("thum" + id);
		if(thum == null) return;
		
		if(isSelect){
			thum.className = "thum_select";
		}else{
			thum.className = "thum";
		}
	},
	
	//-----------------------------------------------------------------------
	//**  onModifyTags
	/**   タグが編集されたときにコールされる
	*      @param	id : ファイルのID
	*      @param	tags : タグ文字列の配列
	*///---------------------------------------------------------------------
	onModifyTags : function(id, tags)
	{
		var elem = $("tag" + id);
		if(elem == null) return;
		
		var str = "";
		
		for(var i=0;i<tags.length;i++){
			str += "<li><a href=\"javascript:wb.find('" + this.htmlDecode(tags[i]) + "')\">" + tags[i] + "</a>";
			str += "<a href=\"javascript:wb.removeTag(" + id + ",'" + this.htmlDecode(tags[i]) + "')\" class=\"a_remove\">[x]</a>";
		}
		
		elem.innerHTML = str;
	},
	
	//-----------------------------------------------------------------------
	//**  onModifyScore
	/**   スコアが編集されたときにコールされる
	*      @param	id : ファイルのID
	*      @param	score : スコア値
	*///---------------------------------------------------------------------
	onModifyScore : function(id, score)
	{
		var elem = $("score" + id);
		if(elem == null) return;
		
		elem.innerHTML = score;
	},
	
	//-----------------------------------------------------------------------
	//**  onModifyField
	/**   特殊フィールドが一つでも変更されたときにコールされる
	*      @param	id : ファイルのID
	*///---------------------------------------------------------------------
	onModifyField : function(id)
	{
		
		
	},
	
	//-----------------------------------------------------------------------
	//**  onExec
	/**   ファイルが実行されたときにコールされる
	*     @param	id : ファイルのID
	*     @param	playerID : プレイヤーID、0:メイン、1～:サブ
	*                          -1：プログラムから開く
	*     @param	start : 開始秒数
	*     @return   0 : デフォルトの動作
	 *              >0 : デフォルトの動作を抑制
	*///---------------------------------------------------------------------
	onExec : function(id, playerID, start)
	{
		return 0;
	},
	
	//-----------------------------------------------------------------------
	//**  onModifyPath
	/**   ファイルのパスが編集されたときにコールされる
	*      @param	id : ファイルのID
	*      @param	drive : ファイルがあるドライブ
	*      @param	dir : ファイルがあるフォルダ
	*      @param	title : ファイルの新しいタイトル
	*      @param	ext : ファイルの新しい拡張子
	*      @param	kana : ファイルの新しい振り仮名
	*///---------------------------------------------------------------------
	onModifyPath : function(id, drive, dir, title, ext, kana)
	{
		var elem = $("title" + id);
		if(elem == null) return;
		
		elem.innerHTML = title + ext;
	},
	
	//-----------------------------------------------------------------------
	//**  onExtensionUpdated
	/**   メインウィンドウで変更があったときにコールされる
	*     エクステンション情報ウィンドウ専用
	*///---------------------------------------------------------------------
	onExtensionUpdated : function()
	{
		
		
	},
	
	//-----------------------------------------------------------------------
	//**  find
	/**   keyを検索ボックスに入れて、直ちに検索を行う
	*     @param	key : 検索文字列
	*///---------------------------------------------------------------------
	find : function(key)
	{
		this.execCmd('find', {'key':key});
	},
	
	//-----------------------------------------------------------------------
	//**  sort
	/**   並び順番を変更して再検索を行う
	*     @param	pri : ソート名(並びバーにある文字列)
	*///---------------------------------------------------------------------
	sort : function(pri)
	{
		this.execCmd('sort', {'pri':pri});
	},
	
	//-----------------------------------------------------------------------
	//**  update
	/**   今の検索条件で再検索を行う
	*     @param   from : 検索結果開始値
	*     @param   count : 開始値からいくつ表示するか、-1でfromからの検索結果をすべて表示
	*                      seamless-scrollがoffのときのみ有効
	*     @param   param : 任意な文字列、onUpdate(mvs, param)のparamに渡される
	*///---------------------------------------------------------------------
	update : function(from, count, param)
	{
		if(from == null) from = 0;
		if(count == null) count = -1;
		if(param == null) param = '';
		
		this.execCmd('update', {'from':from,'count':count,'param':param});
	},
	
	//-----------------------------------------------------------------------
	//**  exec
	/**   指定ファイルを標準プレイヤーで実行する
	*     @param	mvID : ファイルID
	*                      0でいま選択中のファイルが使われる
	*     @param	playerID : プレイヤーID、0:メイン、1～:サブ
	*     @param	start : 開始秒数
	*///---------------------------------------------------------------------
	exec : function(mvID, playerID, start)
	{
		switch(arguments.length){
			// 引数３つのときそのままの順番で実行
			case 3: this.execCmd('exec', {'mv':mvID,'player':playerID,'start':start}); break;
			
			// 引数２つのときその引数をIDとPlayerとして実行
			case 2: this.execCmd('exec', {'mv':mvID,'player':playerID,'start':0}); break;
			
			// 引数１つのときその引数をPlayerとして実行
			case 1: this.execCmd('exec', {'mv':0,'player':mvID,'start':0}); break;
		}
	},
	
	//-----------------------------------------------------------------------
	//**  execBookmark
	/**   指定ブックマークの関連するファイルをプレイヤーで実行する
	*     @param	id : ブックマークID
	*     @param	playerID : プレイヤーID、0:メイン、1～:サブ
	*///---------------------------------------------------------------------
	execBookmark : function(id, playerID)
	{
		if(playerID == null) playerID = 0;
		
		this.execCmd('execBookmark', {'id':id,'player':playerID});
	},
	
	//-----------------------------------------------------------------------
	//**  deleteBookmark
	/**   指定ブックマークファイルを削除（※ブックマークの実ファイルも削除される）
	*     @param	id : ブックマークID
	*///---------------------------------------------------------------------
	deleteBookmark : function(id)
	{
		this.execCmd('deleteBookmark', {'id':id});
	},
	
	//-----------------------------------------------------------------------
	//**  copy
	/**   指定ファイルを指定フォルダにコピー
	*     @param	mvID : ファイルID
	*                      指定しないもしく0でいま選択中のファイルが使われる
	*     @param	dir : コピー先フォルダのパス
	*     @param	msg : 1: 問い合わせのダイアログを出す(デフォルト)
	*                     0: 問い合わせのダイアログを出さない
	*                     ただし、スキンから実行された場合は必ずダイアログが出ます
	*///---------------------------------------------------------------------
	copy : function(mvID, dir, msg)
	{
		switch(arguments.length){
			// 引数３つのときそのままの順番で実行
			case 3: this.execCmd('copy', {'mv':mvID,'dir':dir,'msg':msg}); break;
			
			// 引数２つのときその引数をDirとMsgとして実行
			case 2: this.execCmd('copy', {'mv':0,'dir':mvID,'msg':dir}); break;
			
			// 引数１つのときその引数をDirとして実行
			case 1: this.execCmd('copy', {'mv':0,'dir':mvID,'msg':1}); break;
		}
	},
	
	//-----------------------------------------------------------------------
	//**  move
	/**   指定ファイルを指定フォルダに移動
	*     @param	mvID : ファイルID
	*                      指定しないもしく0でいま選択中のファイルが使われる
	*     @param	dir : 移動先フォルダのパス
	*     @param	msg : 1: 問い合わせのダイアログを出す(デフォルト)
	*                     0: 問い合わせのダイアログを出さない
	*                     ただし、スキンから実行された場合は必ずダイアログが出ます
	*///---------------------------------------------------------------------
	move : function(mvID, dir, msg)
	{
		switch(arguments.length){
			// 引数３つのときそのままの順番で実行
			case 3: this.execCmd('move', {'mv':mvID,'dir':dir,'msg':msg}); break;
			
			// 引数２つのときその引数をDirとMsgとして実行
			case 2: this.execCmd('move', {'mv':0,'dir':mvID,'msg':dir}); break;
			
			// 引数１つのときその引数をDirとして実行
			case 1: this.execCmd('move', {'mv':0,'dir':mvID,'msg':1}); break;
		}
	},
	
	//-----------------------------------------------------------------------
	//**  scrollTo
	/**   指定ファイルが画面内に入るようにスクロール
	*     seamless-scrollがonのときのみ有効
	*     @param	mvID : ファイルID
	*                      0でいまフォーカスを持つファイルが使われる
	*///---------------------------------------------------------------------
	scrollTo : function(mvID)
	{
		this.execCmd('scrollTo', {'mv':mvID});
	},
	
	//-----------------------------------------------------------------------
	//**  addWhere
	/**   検索条件に新しい条件を追加する
	*     @param   where : デフォルトの検索条件にANDで追加するSQL条件
	*              例：movie_path like '%foo%'
	*///---------------------------------------------------------------------
	addWhere : function(where)
	{
		this.execCmd('addWhere', {'where':where});
	},
	
	//-----------------------------------------------------------------------
	//**  addOrder
	/**   並び条件に新しい条件を追加する
	*     @param   order : 並びバーにある文字列
	*                      文字列を { } で囲むとその文字列をSQLのORDER文として実行
	*                      追加した条件をクリアしたい場合は空白文字列をorderに入れて実行
	*     @param   override : 0:並びバーの並びでソート後さらにこの条件でソートする
	*                         1:並びバーの並び条件に上書き
	*     @notice  設定後にユーザーが並びバーで並びを変更すると、この設定は自動的クリアされます。
	*///---------------------------------------------------------------------
	addOrder : function(order, override)
	{
		if(override == null) override = 0;
		
		this.execCmd('addOrder', {'order':order,'override':override});
	},
	
	//-----------------------------------------------------------------------
	//**  makeThum
	/**   指定サイズのサムネイルを作成＆表示
	*     @param	elemID : サムネイルを表示するimgタグのid名
	*     @param	mvID : ファイルID
	*     @param	width : 幅
	*     @param	height : 高さ
	*     @param	column : 横の枚数
	*     @param	row : 縦の枚数
	*     @param	isRandom : ランダムフレーム(true)、等間隔フレーム(false)
	*     @param	isCheckExist : すでに作られているなら新たに作らない(true)
	*     @return   すでにそのサムネイルは作られたかどうか
	*///---------------------------------------------------------------------
	makeThum : function(elemID, mvID, width, height, column, row, isRandom, isCheckExist)
	{
		return this.execCmd('makeThum', {'elem':elemID,'mv':mvID,'w':width,'h':height,'c':column,'r':row,'random':isRandom?1:0,'exist':isCheckExist?1:0});
	},
	
	//-----------------------------------------------------------------------
	//**  focusThum
	/**   指定ファイルをフォーカス状態にする
	*     @param	mvID : ファイルID
	*///---------------------------------------------------------------------
	focusThum : function(mvID)
	{
		this.execCmd('focusThum', {'mv':mvID});
	},
	
	//-----------------------------------------------------------------------
	//**  selectThum
	/**   指定ファイルの選択状態を変更する
	*     @param	mvID : ファイルID
	*     @param	isSelect : 選択状態かどうか
	*///---------------------------------------------------------------------
	selectThum : function(mvID, isSelect)
	{
		if(isSelect == null) isSelect = 1;
		
		this.execCmd('selectThum', {'mv':mvID,'sel':isSelect});
	},
	
	//-----------------------------------------------------------------------
	//**  updateInfo
	/**   指定ファイルのコーデックなどの情報を再更新
	*     @param	mvID : ファイルID
	*                      省略もしく0でいま選択中のファイルが使われる
	*///---------------------------------------------------------------------
	updateInfo : function(mvID)
	{
		if(mvID == null) mvID = 0;
		
		this.execCmd('updateInfo', {'mv':mvID});
	},
	
	//-----------------------------------------------------------------------
	//**  appCmd
	/**   アプリケーションコマンドを実行
	*     @param	appID : コマンドID
	*///---------------------------------------------------------------------
	appCmd : function(appID)
	{
		this.execCmd('appCmd', {'id':appID});
	},
	
	//-----------------------------------------------------------------------
	//**  showContextMenu
	/**   コンテキストメニューを表示
	*     @param	mvID : ファイルID
	*                      省略もしく0でいまフォーカスを持つファイルが使われる
	*///---------------------------------------------------------------------
	showContextMenu : function(mvID)
	{
		if(mvID == null) mvID = 0;
		
		this.execCmd('showContextMenu', {'mv':mvID});
	},
	
	//-----------------------------------------------------------------------
	//**  showTextMenu
	/**   テキストユーティリティメニューを表示
	*     @param	text : テキスト
	*///---------------------------------------------------------------------
	showTextMenu : function(text)
	{
		this.execCmd('showTextMenu', {'text':text});
	},
	
	//-----------------------------------------------------------------------
	//**  switchDB
	/**   他の管理ファイルを開く
	*     @param   name : 管理ファイルの絶対パス(拡張子は省略可)
	*                     ディレクトリを書かなければEXEと同じフォルダにあるとみなす
	*     @param   newWnd : 新しいウィンドウで開くかどうか(異なる管理ファイルのみ適用可能)
	*///---------------------------------------------------------------------
	switchDB : function(name, newWnd)
	{
		if(newWnd == null) newWnd = 0;
		
		this.execCmd('switchDB', {'name':name,'new':newWnd});
	},
	
	//-----------------------------------------------------------------------
	//**  changeSkin
	/**   他のスキンに切り替え
	*     @param	name : スキン名
	*///---------------------------------------------------------------------
	changeSkin : function(name)
	{
		this.execCmd('changeSkin', {'name':name});
	},
	
	//-----------------------------------------------------------------------
	//**  changeExtension
	/**   他のエクステンションに切り替え
	*     @param	name : エクステンション名
	*///---------------------------------------------------------------------
	changeExtension : function(name)
	{
		this.execCmd('changeExtension', {'name':name});
	},
	
	//-----------------------------------------------------------------------
	//**  addPath
	/**   指定フォルダorファイルにあるファイルを登録する
	*     フォルダの場合、サブフォルダ内も登録対象になる
	*     @param	path : フォルダもしくファイルのパス
	*     @param	sub : サブフォルダもチェックするかどうか
	*///---------------------------------------------------------------------
	addPath : function(path, sub)
	{
		this.execCmd('addPath', {'path':path,'sub':sub});
	},
	
	//-----------------------------------------------------------------------
	//**  addScore
	/**   指定ファイルのスコアを加算
	*     @param	mvID : ファイルID
	*                      省略もしく0でいま選択中のファイルが使われる
	*     @param	add : 追加(マイナスで減少)する量
	*///---------------------------------------------------------------------
	addScore : function(mvID, add)
	{
		if(arguments.length == 2){
			this.execCmd('addScore', {'mv':mvID,'add':add});
		}else{
			this.execCmd('addScore', {'mv':0,'add':mvID});
		}
	},
	
	//-----------------------------------------------------------------------
	//**  modifyField
	/**   指定特殊フィールドの情報を書き換え
	*     @param	mvID : ファイルID
	*                      省略もしく0でいま選択中のファイルが使われる
	*     @param	field : フィールドの名前(文字列)
	*                       特殊フィールド一覧
	*                         - 'comment1' ～ 'comment3'
	*     @param	value : 新しい値
	*///---------------------------------------------------------------------
	modifyField : function(mvID, field, value)
	{
		if(arguments.length == 3){
			this.execCmd('modifyField', {'mv':mvID,'field':field,'value':value});
		}else{
			this.execCmd('modifyField', {'mv':0,'field':mvID,'value':field});
		}
	},
	
	//-----------------------------------------------------------------------
	//**  addTag
	/**   指定ファイルにタグを登録する 
	*     @param	mvID : ファイルID
	*                      省略もしく0でいま選択中のファイルが使われる
	*     @param	tag : 追加するタグの文字列
	*///---------------------------------------------------------------------
	addTag : function(mvID, tag)
	{
		if(arguments.length == 2){
			this.execCmd('addTag', {'mv':mvID,'tag':tag});
		}else{
			this.execCmd('addTag', {'mv':0,'tag':mvID});
		}
	},
	
	//-----------------------------------------------------------------------
	//**  removeTag
	/**   指定ファイルのタグを削除
	*     @param	mvID : ファイルID
	*                      省略もしく0でいま選択中のファイルが使われる
	*     @param	tag : 削除するタグの文字列
	*///---------------------------------------------------------------------
	removeTag : function(mvID, tag)
	{
		if(arguments.length == 2){
			this.execCmd('removeTag', {'mv':mvID,'tag':tag});
		}else{
			this.execCmd('removeTag', {'mv':0,'tag':mvID});
		}
	},
	
	//-----------------------------------------------------------------------
	//**  flipTag
	/**   指定ファイルに指定タグがなければ登録、あれば削除
	*     @param	mvID : ファイルID
	*                      省略もしく0でいま選択中のファイルが使われる
	*     @param	tag : 追加(削除)するタグの文字列
	*///---------------------------------------------------------------------
	flipTag : function(mvID, tag)
	{
		if(arguments.length == 2){
			this.execCmd('flipTag', {'mv':mvID,'tag':tag});
		}else{
			this.execCmd('flipTag', {'mv':0,'tag':mvID});
		}
	},
	
	//-----------------------------------------------------------------------
	//**  addFilter
	/**   フィルタを追加
	*     @param	filter : 追加するフィルタの文字列
	*///---------------------------------------------------------------------
	addFilter : function(filter)
	{
		this.execCmd('addFilter', {'filter':filter});
	},
	
	//-----------------------------------------------------------------------
	//**  removeFilter
	/**   指定フィルタを削除
	*     @param	filter : 削除するフィルタの文字列
	*///---------------------------------------------------------------------
	removeFilter : function(filter)
	{
		this.execCmd('removeFilter', {'filter':filter});
	},
	
	//-----------------------------------------------------------------------
	//**  clearFilter
	/**   登録されているすべてのフィルタをクリア
	*///---------------------------------------------------------------------
	clearFilter : function()
	{
		this.execCmd("clearFilter?");
	},
	
	//-----------------------------------------------------------------------
	//**  writeProfile
	/**   DBにスキンの固有情報を書き込む
	*     @param	key : キー名
	*     @param	value : キー値、2000文字以内
	*///---------------------------------------------------------------------
	writeProfile : function(key, value)
	{
		this.execCmd('writeProfile', {'key':key,'value':value});
	},
	
	//-----------------------------------------------------------------------
	//**  scrollSetting
	/**   シームレススクロールの切り替え
	 *    @param   type : 0:OFF、1:下方向のみ、2:双方向
	 *    @param   scrollID : スクロールするdivのID
	*///---------------------------------------------------------------------
	scrollSetting : function(type, scrollID)
	{
		if(scrollID == null) scrollID = '';
		
		this.execCmd('scrollSetting', {'type':type,'id':scrollID});
	},
	
	//-----------------------------------------------------------------------
	//**  thumSetting
	/**   生成するサムネイルの設定を変更
	 *    @param   width : サムネイルの幅、デフォルト200 
	 *    @param   height : サムネイルの高さ、デフォルト150
	 *    @param   column : 横に何枚サムネイルを並べるか
	 *    @param   row : 縦に何枚サムネイルを並べるか
	*///---------------------------------------------------------------------
	thumSetting : function(width, height, column, row)
	{
		this.execCmd('thumSetting', {'w':width,'h':height,'c':column,'r':row});
	},
	
	//-----------------------------------------------------------------------
	//**  writeFile
	/**   指定ファイルに一行のテキストを書き出す
	*     @param   name : ファイルの名前(例：foo.txt)
	*			 		  スキン内からの呼び出しはスキンフォルダに出力されされる
	*					   それ以外のところからの呼び出しはwb\tempフォルダに出力される
	*     @param   line : 書き込むテキスト
	*     @param   truncate : 0:ファイルの最後に追加で書く
	*                         1:ファイルをクリアしてから書く
	*     @return  テキストを書き込んだファイルのフルパス(空白はエラー)
	*///---------------------------------------------------------------------
	writeFile : function(name, line, truncate)
	{
		if(truncate == null) truncate = 0;
		
		return this.execCmd('writeFile', {'name':name,'line':line,'truncate':truncate});
	},
	
	//-----------------------------------------------------------------------
	//**  readFile
	/**   指定ファイルにあるテキストを取得
	*     @param   name : ファイルの名前(例：foo.txt)
	*     @return  取得できれば行数分のテキスト配列
	*              ファイルがなければ空白の配列を返す
	*			   スキン内からの呼び出しはスキンフォルダ
	*			   それ以外のところからの呼び出しはwb\tempフォルダが対象
	*///---------------------------------------------------------------------
	readFile : function(name)
	{
		return eval("(" + this.execCmd('readFile', {'name':name}) + ")");
	},
	
	//-----------------------------------------------------------------------
	//**  execFile
	/**   指定ファイルをプレイヤーで実行
	*     @param   name : ファイルの名前(例：foo.txt)
	*              ファイルがなければなにも処理されない
	*			   スキン内からの呼び出しはスキンフォルダ
	*			   それ以外のところからの呼び出しはwb\tempフォルダが対象
	*     @param   playerID : プレイヤーID、0:メイン、1～:サブ
	*///---------------------------------------------------------------------
	execFile : function(name, playerID)
	{
		if(playerID == null) playerID = 0;
		
		this.execCmd('execFile', {'name':name,'player':playerID});
	},
	
	//-----------------------------------------------------------------------
	//**  deleteFile
	/**   指定ファイルを削除
	*     @param   name : ファイルの名前(例：foo.txt)
	*              ファイルがなければなにも処理しない
	*			   スキン内からの呼び出しはスキンフォルダ
	*			   それ以外のところからの呼び出しはwb\tempフォルダが対象
	*///---------------------------------------------------------------------
	deleteFile : function(name)
	{
		this.execCmd('deleteFile', {'name':name});
	},
	
	//-----------------------------------------------------------------------
	//**  checkFile
	/**   指定ファイルが存在するかどうかを取得
	*     @param   path : ファイルのフルパス
	 *     @return  1:ある、0:なし
	*///---------------------------------------------------------------------
	checkFile : function(path)
	{
		return this.execCmd('checkFile', {'path':path});
	},
	
	//-----------------------------------------------------------------------
	//**  switchKey
	/**   キーマップ切り替え
	 *    @param   name : WhiteBrowser.exeと同じフォルダにあるwhkファイルの名前
	*///---------------------------------------------------------------------
	switchKey : function(name)
	{
		this.execCmd('switchKey', {'name':name});
	},
	
	//-----------------------------------------------------------------------
	//**  getArgv
	/**   起動コマンドラインで渡された引数を取得
	*     @param   id : 引数番号(1～9)
	*              それぞれコマンドラインオプションの -1 ～ -9 に対応
	*     @return  引数文字列
	*///---------------------------------------------------------------------
	getArgv : function(id)
	{
		return this.execCmd('getArgv', {'id':id});
	},
	
	//-----------------------------------------------------------------------
	//**  getTagletName
	/**   実行したタグレットの名前を取得 
	*      @return  タグレットの名前
	*///---------------------------------------------------------------------
	getTagletName : function()
	{
		return $('taglet').innerText;
	},
	
	//-----------------------------------------------------------------------
	//**  getDBName
	/**   wbファイルの名前を取得 
	*      @return  wbファイルの名前
	*///---------------------------------------------------------------------
	getDBName : function()
	{
		return this.execCmd("getDBName?");
	},
	
	//-----------------------------------------------------------------------
	//**  getAppDir
	/**   WhiteBrowser.exeがあるフォルダのパスを取得 
	*      @return  フォルダのパス
	*///---------------------------------------------------------------------
	getAppDir : function()
	{
		return this.execCmd("getAppDir?");
	},
	
	//-----------------------------------------------------------------------
	//**  getThumDir
	/**   サムネイル格納フォルダのルートパスを取得 
	*      @return  フォルダのパス
	*///---------------------------------------------------------------------
	getThumDir : function()
	{
		return this.execCmd("getThumDir?");
	},
	
	//-----------------------------------------------------------------------
	//**  getSkinName
	/**   いまのスキンの名前を取得 
	*      @return  スキンの名前
	*///---------------------------------------------------------------------
	getSkinName : function()
	{
		return this.execCmd("getSkinName?");
	},
	
	//-----------------------------------------------------------------------
	//**  getExtensionName
	/**   いまのエクステンションの名前を取得 
	*      @return  エクステンションの名前
	*///---------------------------------------------------------------------
	getExtensionName : function()
	{
		return this.execCmd("getExtensionName?");
	},
	
	//-----------------------------------------------------------------------
	//**  getFocusThum
	/**   フォーカスを持つファイルのIDを取得
	*      @return  ファイルID
	*///---------------------------------------------------------------------
	getFocusThum : function()
	{
		return this.execCmd("getFocusThum?");
	},
	
	//-----------------------------------------------------------------------
	//**  getSelectThums
	/**   選択中のすべてのファイルのIDを取得
	*      @return  ファイルID配列
	*///---------------------------------------------------------------------
	getSelectThums : function()
	{
		return eval("(" + this.execCmd("getSelectThums?") + ")");
	},
	
	//-----------------------------------------------------------------------
	//**  getFindInfo
	/**   検索用情報を取得
	*      @return  検索用情報が入っているオブジェクト
	*///---------------------------------------------------------------------
	getFindInfo : function()
	{
		return eval("(" + this.execCmd("getFindInfo?") + ")");
	},
	
	//-----------------------------------------------------------------------
	//**  getInfo
	/**   指定ファイルの情報を取得
	*     連続して複数回取得したい場合ではパフォーマンスが悪いのでgetInfosをお使いください
	*     @param	mvID : ファイルID
	*                      指定しないもしく0でいまフォーカスを持つファイルが使われる
	*     @return   指定ファイルの全情報
	*///---------------------------------------------------------------------
	getInfo : function(mvID)
	{
		if(mvID == null) mvID = 0;
		
		var ret = this.execCmd('getInfo', {'id':mvID});
		if(ret == "") return null;
		
		return eval("(" + ret + ")");
	},
	
	//-----------------------------------------------------------------------
	//**  getInfos
	/**   指定条件に該当するすべてのファイルの情報を取得
	*     @param    from : 検索結果開始値
	*     @param    count : 開始値からいくつ表示するか、-1でfromからの検索結果をすべて表示
	*     @param	where : 追加条件(SQL)
	*     @param	order : 追加並び条件(SQL)
	*     @param	override : 0:いまの検索条件と追加条件をマージして検索
	*                          1:追加条件のみで検索
	*     @return   指定した条件に該当する全ファイルの情報
	*///---------------------------------------------------------------------
	getInfos : function(from, count, where, order, override)
	{
		if(where == null) where = '';
		if(order == null) order = '';
		if(override == null) override = 0;
		
		return eval("(" + this.execCmd('getInfos', {'where':where,'order':order,'from':from,'count':count,'override':override}) + ")");
	},
	
	//-----------------------------------------------------------------------
	//**  getBookmarks
	/**   指定条件に該当するすべてのブックマークの情報を取得
	*     @param    from : 検索結果開始値
	*     @param    count : 開始値からいくつ表示するか、-1でfromからの検索結果をすべて表示
	*     @param	where : 追加条件(SQL)
	*     @param	order : 追加並び条件(SQL)
	*     @param	override : 0:いまの検索条件と追加条件をマージして検索
	*                          1:追加条件のみで検索
	*                          2:いまの検索条件で検索、追加並び条件で並び替え
	*     @return   指定した条件に該当する全ブックマークの情報
	*///---------------------------------------------------------------------
	getBookmarks : function(from, count, where, order, override)
	{
		if(where == null) where = '';
		if(order == null) order = '';
		if(override == null) override = 0;
		
		return eval("(" + this.execCmd('getBookmarks', {'where':where,'order':order,'from':from,'count':count,'override':override}) + ")");
	},
	
	//-----------------------------------------------------------------------
	//**  getTimecode
	/**   指定画像のタイムコード情報を取得
	*     @param	imgID : 画像を表示する<img>タグのID
	*                       指定しないもしく空白でいま選択中のファイルのサムネイル画像が使われる
	*     @param	x : 画像の左上からの相対座標(offsetX)
	*     @param	y : 画像の左上からの相対座標(offsetY)
	*     @return   x,y指定無し：指定画像に含まれているシーンジャンプ用時間情報(秒)の配列
	*               x,y指定あり：指定画像の指定座標の時間情報(秒)
	*///---------------------------------------------------------------------
	getTimecode : function(imgID, x, y)
	{
		if(imgID == null) imgID = '';
		
		if(arguments.length == 3){
			return eval("(" + this.execCmd('getTimecode', {'img':imgID,'x':x,'y':y}) + ")");
		}else{
			return eval("(" + this.execCmd('getTimecode', {'img':imgID}) + ")");
		}
	},
	
	//-----------------------------------------------------------------------
	//**  getWatchList
	/**   監視フォルダリストを取得
	*      @return  監視フォルダのオブジェクト配列
	*///---------------------------------------------------------------------
	getWatchList : function()
	{
		return eval("(" + this.execCmd("getWatchList?") + ")");
	},
	
	//-----------------------------------------------------------------------
	//**  getFileList
	/**   ファイルシステムの指定フォルダ下にあるファイルを取得
	*     @param   dir : ディレクトリパス
	*					 セパレータは \ ではなく、/ を使用してください)
	*     @param   filter : ファイルフィルタ(例：*.*, *.avi...)
	*     @return  ファイル名のオブジェクト配列
	*///---------------------------------------------------------------------
	getFileList : function(dir, filter)
	{
		if(filter == null) filter = "*.*";
		
		return eval("(" + this.execCmd('getFileList', {'dir':dir,'filter':filter}) + ")");
	},
	
	//-----------------------------------------------------------------------
	//**  getProfile
	/**   DBに書き込んだスキンの固有情報を取得
	*      @param	key : キー名
	*      @param	def : デフォルト値
	*      @return  キー値
	*///---------------------------------------------------------------------
	getProfile : function(key, def)
	{
		return this.execCmd('getProfile', {'key':key,'def':def});
	},
	
	//-----------------------------------------------------------------------
	//**  getRelation
	/**   textと関連あると思われるファイル一覧を取得
	*      @param	text : 
	*      @param	limit : 取得上限数
	*      @param	order : 追加ソート順(SQL指定、最初は必ず計算されたランクでソートされる)
	*///---------------------------------------------------------------------
	getRelation : function(text, limit, order)
	{
		if(limit == null) limit = 10;
		if(order == null) order = "last_date DESC";
		
		var segmenter = new TinySegmenter();
		var segs = segmenter.segment(this.htmlDecode(text));
		
		for(var i=0;i<segs.length;i++){
			segs[i] = segs[i].replace(/[ \/\\:;.\-0-9!"#$%&'()\[\]*+?<>_=~^|{}]/g, '');
			segs[i] = segs[i].replace(/[　／￥：；．・０-９！”＃＄％＆’（）「」＊＋？＜＞＿＝～｜｛｝]/g, '');
		}
		
		return eval("(" + this.execCmd("getRelation", {'text':segs.join("/"),'limit':limit,'order':order}) + ")");
	},
	
	//-----------------------------------------------------------------------
	//**  imageBox
	/**   イメージボックスを表示
	*     @param	src : 画像パス
	*///---------------------------------------------------------------------
	imageBox : function(src)
	{
		this.execCmd('imageBox', {'src':src});
	},
	
	imageBoxImpl : function(src)
	{
		var wil = $("wbimglayer");
		
		if(wil == null){
			new Insertion.Top(document.body, '<div id="wbimgbox"></div><div id="wbimglayer"><img id="wbimgimg" src="' + src + '"/></div>');
			
			Position.prepare();
			
			Element.setStyle($("wbimglayer"), {
				'position':'absolute',
				'z-index':'251',
			    'top':Position.deltaY+20,
			    'left':20
			});
			
			Element.setStyle($("wbimgbox"), {
				'position':'absolute',
				'top':Position.deltaY,
				'z-index':'250',
				'left':'0',
				'height':'100%',
				'width':'100%',
				'background':'black',
				'opacity':'0.80',
				'filter':'alpha(opacity=80)'
			});
			
			Event.observe('wbimgbox', 'click', 
				function(){
					Element.remove($('wbimglayer'));
					Element.remove($('wbimgbox'));
				}
			);
			
			Event.observe('wbimglayer', 'click', 
				function(){
					Element.remove($('wbimglayer'));
					Element.remove($('wbimgbox'));
				}
			);
		}else{
			wii.src = src;
		}
	},
	
	//-----------------------------------------------------------------------
	//**  infoBar
	/**   テキストバーを表示
	*     @param	text : 表示するテキスト
	*     @param	hideSec : 何秒後に自動的消えるか。0でユーザーがクリックするまで消えない
	*///---------------------------------------------------------------------
	infoBar : function(text, hideSec)
	{
		if(hideSec == null) hideSec = 4;
		
		this.execCmd('infoBar', {'text':text, 'hide':hideSec});
	},
	
	barTimerID : 0,
	
	infoBarImpl : function(text, hideSec)
	{
		var wbl = $("wbbarlayer");
		
		if(wbl == null){
			new Insertion.Top(document.body, '<div id="wbbarlayer"><p id="wbbartext"></p></div>');
			
			Position.prepare();
			
			Element.setStyle($("wbbarlayer"), {
				'position':'absolute',
				'z-index':'251',
			    'top':'0',
			    'left':'0',
				'width':'100%',
				'padding':'10px',
				'background-color':'black'
			});
			
			Element.setStyle($("wbbartext"), {
				'color':'white',
				'font-size':'9pt',
				'line-height':'1.3em'
			});
			
			Event.observe('wbbarlayer', 'click', 
				function(){
					Element.remove($('wbbarlayer'));
				}
			);
		}
		
		var wbt = $("wbbartext");
		
		if(wbt != null){
			wbt.innerHTML = text;
		}
		
		if(this.barTimerID){
			clearTimeout(this.barTimerID);
			this.barTimerID = 0;
		}
		
		if(hideSec > 0){
			this.barTimerID = setTimeout(function() { 
				if($('wbbarlayer') != null) { Element.remove($('wbbarlayer')); }
				this.barTimerID = 0;
			}, hideSec * 1000);
		}
	},
	
	//-----------------------------------------------------------------------
	//**  execScript
	/**   スクリプトをそのまま実行(必ずメインで実行される)
	*     @param	script : javaScriptコード
	*///---------------------------------------------------------------------
	execScript : function(script)
	{
		this.execCmd('execScript', {'script':script});
	},
	
	//-----------------------------------------------------------------------
	//**  execCmd
	/**   wbのAPIを直接呼び出す
	*     @param	func : API関数名
	*     @param	argv : 連想配列
	*///---------------------------------------------------------------------
	execCmd : function(func, argv)
	{
		var cmd = func + "?";
		
		for(pro in argv){
			var arg = argv[pro] + "";
			
			arg = arg.replace(/ /g, "%20");
			arg = arg.replace(/&/g, "%26");
			arg = arg.replace(/=/g, "%3d");
			
			cmd += pro + "=" + arg + "&";
		}
		
		return window.external.execCmd(cmd);
	},
	
	//-----------------------------------------------------------------------
	//**  trace
	/**   実行情報ペインに一行テキストを出力します
	*     @param	text : 出力テキスト
	*     @param	type : 0:通常、1:警告、2:エラー
	*///---------------------------------------------------------------------
	trace : function(text, type)
	{
		if(type == null) type = 0;
		
		this.execCmd('trace', {'text':text,'type':type});
	},
	
	//-----------------------------------------------------------------------
	//**  allProperty
	/**   オブジェクトのプロパティを文字列で返す
	*      @param	obj : オブジェクト
	*      @return  プロパティ一覧文字列
	*///---------------------------------------------------------------------
	allProperty : function(obj)
	{
		var txt = "";
		var count = 0;
		
		for(pro in obj){
			txt += pro + " = " + obj[pro] + ";";
			
			if(++count % 8 == 0) txt += "\n";
		}
		
		return txt;
	},
	
	//-----------------------------------------------------------------------
	//**  htmlDecode
	/**   HTMLエンコードされたパラメータをデコード
	*      @param	str : エンコードされている文字列
	*      @return  デコード済み文字列
	*///---------------------------------------------------------------------
	htmlDecode : function(str)
	{
		str = str.replace(/&nbsp;/g, ' ');
		str = str.replace(/&lt;/g, '<');
		str = str.replace(/&gt;/g, '>');
		str = str.replace(/&quot;/g, '"');
		str = str.replace(/&amp;/g, '&');
		str = str.replace(/'/g, '\\\'');
		
		return str;
	}
}; // wbsuper.prototype

/**
   white browser base class
*/
var wbbase = Class.create();

wbbase.prototype = Object.extend(new wbsuper, {});

/**
   white browser instance
*/
var wb = new wbbase();

/**
   system bridge function
*/
function __wbversion(){ return wb.app_version; }
function __wbexec(id, playerID, start){ return wb.onExec(id, playerID, start); }

/***
	** superclass function call sample **
	
	wb.onClearAll = function()
	{
		wbsuper.prototype.onClearAll();
	}
*/
