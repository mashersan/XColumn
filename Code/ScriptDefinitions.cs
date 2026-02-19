using System.Text.Json;

namespace XColumn
{
    /// <summary>
    /// アプリケーションで使用するJavaScriptコードやCSS定義を一元管理するクラス。
    /// </summary>
    public static class ScriptDefinitions
    {
        #region CSS Definitions

        /// <summary>
        /// メニューを非表示にするCSS定義。
        /// </summary>
        public const string CssHideMenu = "header[role=\"banner\"] { display: none !important; } main[role=\"main\"] { align-items: flex-start !important; }";

        /// <summary>
        /// リストヘッダーを非表示にするCSS定義。
        /// </summary>
        public const string CssHideListHeader = @"
            [data-testid='primaryColumn'] div:has([data-testid='editListButton']):not(:has([data-testid='cellInnerDiv'])),
            [data-testid='primaryColumn'] div:has(a[href$='/members']):not(:has([data-testid='cellInnerDiv'])),
            [data-testid='primaryColumn'] div:has(a[href$='/followers']):not(:has([data-testid='cellInnerDiv'])) { 
                display: none !important; 
            }
            [data-testid='primaryColumn'] [data-testid='app-bar-back-button'] { display: none !important; }
            [data-testid='primaryColumn'] { padding-top: 0 !important; }
        ";

        /// <summary>
        /// 右サイドバーを非表示にするCSS定義。
        /// </summary>
        public const string CssHideRightSidebar = "[data-testid='sidebarColumn'] { display: none !important; }";

        /// <summary>
        /// リツイートや引用ツイートのソーシャルコンテキストを非表示にするCSS定義。
        /// </summary>
        public const string CssHideSocialContext = @"
            div[data-testid='cellInnerDiv']:has([data-testid='socialContext']),
            .tweet-context,
            .retweet-credit,
            .js-retweet-text { display: none !important; }
        ";

        /// <summary>
        /// リプライを非表示にするCSS定義。
        /// </summary>
        public const string CssHideRepliesClass = ".xcolumn-is-reply { display: none !important; }";

        #endregion

        #region JavaScript Definitions

        /// <summary>
        /// キー入力検知スクリプト (ESCキー対応)
        /// </summary>
        public const string ScriptDetectKeyInput = @"
            (function() {
                if (window.xColumnKeyHook) return;
                window.xColumnKeyHook = true;
                document.addEventListener('keydown', (e) => {
                    // ESCキーが押された場合
                    if (e.key === 'Escape') {
                        // ダイアログ（画像表示、ツイート作成画面など）が開いているかチェック
                        const dialogs = document.querySelectorAll('div[role=""dialog""]');
                        let hasVisibleDialog = false;
                        for (const d of dialogs) {
                            if (d.offsetParent !== null) { 
                                hasVisibleDialog = true;
                                break;
                            }
                        }
                        // ダイアログが表示されている場合は、X標準の動作に任せる
                        if (hasVisibleDialog) {
                            return;
                        }
                        // ダイアログがない場合のみ、アプリ側の戻る動作を要求
                        window.chrome.webview.postMessage(JSON.stringify({ type: 'keyInput', key: 'Escape' }));
                    }
                });
            })();
        ";

        /// <summary>
        /// スクロール位置を保存・復元するスクリプト。
        /// </summary>
        public const string ScriptPreserveScrollPosition = @"
            (function() {
                if (window.xColumnScrollRestorer) return;
                window.xColumnScrollRestorer = true;

                // ブラウザ標準の復元機能を無効化
                if ('scrollRestoration' in history) {
                    history.scrollRestoration = 'manual';
                }

                function getKey() {
                    return 'xc_scroll_' + window.location.pathname; 
                }

                // 位置を保存する関数 (0も許容するように修正)
                function saveCurrentPosition() {
                    const y = window.scrollY;
                    sessionStorage.setItem(getKey(), y);
                }

                // スクロール時の保存（デバウンスあり）
                let saveTimer;
                window.addEventListener('scroll', () => {
                    clearTimeout(saveTimer);
                    saveTimer = setTimeout(saveCurrentPosition, 200);
                }, { passive: true });

                // --- 追加: リンククリックや操作時に即座に保存する ---
                // これにより、スクロール直後に画像を開いても現在の位置が確実に保存されます
                ['mousedown', 'touchstart'].forEach(evt => {
                    window.addEventListener(evt, saveCurrentPosition, { passive: true });
                });

                let restoreTimers = [];

                function cancelRestoration() {
                    if (restoreTimers.length > 0) {
                        restoreTimers.forEach(id => clearTimeout(id));
                        restoreTimers = [];
                    }
                }

                ['wheel', 'touchmove', 'keydown', 'mousedown'].forEach(evt => {
                    window.addEventListener(evt, cancelRestoration, { passive: true, capture: true });
                });

                function restorePosition() {
                    cancelRestoration();
                    const key = getKey();
                    const savedY = sessionStorage.getItem(key);
                    
                    if (savedY !== null) {
                        const targetY = parseInt(savedY, 10);
                        if (!isNaN(targetY)) {
                            // 複数回の試行で確実に復元
                            const attempts = [0, 50, 150, 300, 500, 1000, 2000];
                            attempts.forEach(delay => {
                                const timerId = setTimeout(() => {
                                    if (document.body.scrollHeight >= targetY) {
                                        if (Math.abs(window.scrollY - targetY) > 10) {
                                            window.scrollTo(0, targetY);
                                        }
                                    }
                                }, delay);
                                restoreTimers.push(timerId);
                            });
                        }
                    }
                }

                if (document.readyState === 'complete') {
                    restorePosition();
                } else {
                    window.addEventListener('load', restorePosition);
                }

                window.addEventListener('popstate', () => {
                    setTimeout(restorePosition, 50);
                });
                
                const originalPushState = history.pushState;
                history.pushState = function() {
                    originalPushState.apply(this, arguments);
                    setTimeout(restorePosition, 50);
                };
            })();
        ";

        /// <summary>
        /// リプライ検出スクリプト。
        /// </summary>
        public const string ScriptDetectReplies = @"
            (function() {
                if (window.xColumnReplyDetector) return;
                window.xColumnReplyDetector = true;
                const replyKeywords = ['返信先', 'Replying to'];
                function detect() {
                    const cells = document.querySelectorAll('div[data-testid=""cellInnerDiv""]:not(.xcolumn-checked)');
                    cells.forEach(cell => {
                        cell.classList.add('xcolumn-checked');
                        const tweet = cell.querySelector('article[data-testid=""tweet""]');
                        if (!tweet) return;
                        const body = tweet.querySelector('[data-testid=""tweetText""]');
                        const header = tweet.querySelector('[data-testid=""User-Name""]');
                        const walker = document.createTreeWalker(tweet, NodeFilter.SHOW_TEXT, null, false);
                        let node;
                        while(node = walker.nextNode()) {
                            const text = node.textContent;
                            if (replyKeywords.some(kw => text.includes(kw))) {
                                if (body && body.contains(node)) continue;
                                if (header && header.contains(node)) continue;
                                cell.classList.add('xcolumn-is-reply');
                                break;
                            }
                        }
                    });
                }
                setInterval(detect, 500);
            })();
        ";

        /// <summary>
        /// 入力監視スクリプト (IME対応版)
        /// </summary>
        public const string ScriptDetectInput = @"
            (function() {
                if (window.xColumnInputDetector) return;
                window.xColumnInputDetector = true;
                let isComposing = false;
                function notify() {
                    const el = document.activeElement;
                    const isInput = isComposing || (el && (['INPUT', 'TEXTAREA'].includes(el.tagName) || el.isContentEditable));
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'inputState', val: isInput }));
                }
                document.addEventListener('focus', notify, true);
                document.addEventListener('blur', notify, true);
                document.addEventListener('compositionstart', () => { isComposing = true; notify(); }, true);
                document.addEventListener('compositionend', () => { isComposing = false; notify(); }, true);
                notify();
            })();
        ";

        /// <summary>
        /// トレンドクリック時の挙動制御スクリプト
        /// </summary>
        public const string ScriptTrendingClick = @"
            (function() {
                if (window.xColumnTrendingHook) return;
                window.xColumnTrendingHook = true;
                document.addEventListener('click', function(e) {
                    if (!window.location.href.includes('/explore/')) return;
                    const target = e.target;
                    const anchor = target.closest('a');
                    if (anchor) {
                        const href = anchor.getAttribute('href');
                        if (href && (href.includes('/search') || href.includes('q='))) {
                            const fullUrl = new URL(href, window.location.origin).href;
                            postNewColumn(fullUrl);
                            e.preventDefault(); e.stopPropagation();
                            return;
                        }
                    }
                    const trendDiv = target.closest('div[data-testid=""trend""]');
                    if (trendDiv) {
                        const lines = trendDiv.innerText.split('\n');
                        let keyword = '';
                        const ignoreWords = ['トレンド', 'おすすめ', 'さらに表示', 'Show more', 'Topic', 'Promoted'];
                        for (let line of lines) {
                            line = line.trim();
                            if (!line) continue;
                            if (line.startsWith('#')) { keyword = line; break; }
                            if (/^\d+$/.test(line)) continue;
                            if (line.includes('·')) continue;
                            if (/\d/.test(line) && (line.includes('件') || line.includes('posts'))) continue;
                            if (ignoreWords.includes(line)) continue;
                            if (line.endsWith('のトレンド')) continue;
                            if (line === '.' || line === ',') continue;
                            keyword = line;
                            break;
                        }
                        if (keyword) {
                            const searchUrl = 'https://x.com/search?q=' + encodeURIComponent(keyword);
                            postNewColumn(searchUrl);
                            e.preventDefault(); e.stopPropagation();
                        }
                    }
                }, true); 
                function postNewColumn(url) {
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'openNewColumn', url: url }));
                }
            })();
        ";

        /// <summary>
        /// スクロール同期用スクリプト
        /// </summary>
        public const string ScriptScrollSync = @"
            (function() {
                if (window.xColumnScrollHook) return;
                window.xColumnScrollHook = true;
                window.addEventListener('wheel', (e) => {
                    let delta = 0;
                    if (e.shiftKey && e.deltaY !== 0) delta = e.deltaY;
                    else if (e.deltaX !== 0) delta = e.deltaX;
                    if (delta !== 0) {
                        window.chrome.webview.postMessage(JSON.stringify({ type: 'horizontalScroll', delta: delta }));
                        e.preventDefault(); e.stopPropagation();
                    }
                }, { passive: false });
            })();
        ";

        /// <summary>
        /// YouTubeクリック制御スクリプト
        /// </summary>
        public const string ScriptYouTubeClick = @"
            (function() {
                if (window.xColumnYTHook) return;
                window.xColumnYTHook = true;
                document.addEventListener('click', function(e) {
                    const target = e.target;
                    if (!target || !target.closest) return;
                    const card = target.closest('[data-testid=""card.wrapper""]');
                    if (card) {
                        const ytLink = card.querySelector('a[href*=""youtube.com""], a[href*=""youtu.be""]');
                        if (ytLink) {
                            e.preventDefault(); e.stopPropagation();
                            const article = card.closest('article[data-testid=""tweet""]');
                            if (article) {
                                const statusLink = article.querySelector('a[href*=""/status/""]');
                                if (statusLink) window.location.href = statusLink.href;
                            }
                            return;
                        }
                    }
                }, true);
            })();
        ";

        /// <summary>
        /// カラム内でのあらゆる遷移（リンククリック、SPA内部遷移）を横取りし、
        /// カラムの表示を維持したままモーダルでの表示をアプリに要求するスクリプト。
        /// </summary>
        public const string ScriptInterceptClick = @"
    (function() {
        if (window.xColumnInterceptHook) return;
        window.xColumnInterceptHook = true;

        // デバッグ用ログ
        function log(msg) {
            try { window.chrome.webview.postMessage(JSON.stringify({ type: 'debugLog', message: msg })); } catch(e) {}
        }

        const isFocusTarget = (url) => {
            if (!url) return false;
            //return url.includes('/status/') || url.includes('/settings') || url.includes('/compose/') || url.includes('/intent/');
            return url.includes('/status/') || url.includes('/settings');
        };

        const originalReplace = history.replaceState;
        history.replaceState = function(state, title, url) {
            try {
                const fullUrl = new URL(url, window.location.href).href;
                if (isFocusTarget(fullUrl)) {
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'openFocusMode', url: fullUrl }));
                    return;
                }
            } catch(e) {}
            return originalReplace.apply(this, arguments);
        };

        document.addEventListener('click', function(e) {
            const media = e.target.closest('[data-testid=""tweetPhoto""]') || 
                          e.target.closest('[data-testid=""videoPlayer""]') ||
                          e.target.closest('[data-testid=""card.layoutLarge.media""]');

            // 設定で「メディアクリック時の遷移」が無効化されている場合
            // アプリ側での横取りを行わず、X標準の動作（カラム内拡大など）に任せて終了する
            if (media && window.xColumnDisableMediaFocus === true) {
                return;
            }

            let anchor = e.target.closest('a');
            let url = anchor ? anchor.href : null;

            // --- 画像/動画クリック時の補正ロジック ---
            if (media) {
                 log('Media clicked. Initial Found URL: ' + (url || 'null'));
                 
                 const currentTweet = media.closest('article[data-testid=""tweet""]');
                 if (currentTweet) {
                     // このツイートの正規URLを取得
                     const timeEl = currentTweet.querySelector('time');
                     const timeAnchor = timeEl ? timeEl.closest('a') : null;
                     const correctUrl = timeAnchor ? timeAnchor.href : null;
                     
                     log('Current Tweet Context URL: ' + (correctUrl || 'null'));

                     // アンカーチェック
                     const anchorTweet = anchor ? anchor.closest('article[data-testid=""tweet""]') : null;
                     const isAnchorInDifferentTweet = anchorTweet && anchorTweet !== currentTweet;

                     const getId = (u) => {
                         const m = u ? u.match(/\/status\/(\d+)/) : null;
                         return m ? m[1] : 'unknown';
                     };

                     const correctId = getId(correctUrl);
                     const anchorId = getId(url);

                     log(`ID Check -> Correct: ${correctId} vs Anchor: ${anchorId}`);

                     if (!url || isAnchorInDifferentTweet || (correctId !== 'unknown' && anchorId !== correctId)) {
                         log('>> Mismatch or Missing URL. Forcing Correct URL.');
                         url = correctUrl;
                     } else {
                         log('>> Match confirmed.');
                     }
                 }
            }
            // -------------------------------------------

            if (!url) return;

            if (isFocusTarget(url)) {
                if (media && url.includes('/status/') && !url.includes('/photo/') && !url.includes('/video/')) {
                    try {
                        const urlObj = new URL(url);
                        urlObj.pathname = urlObj.pathname.replace(/\/$/, '') + '/photo/1';
                        url = urlObj.toString();
                        log('Converted to Photo URL: ' + url);
                    } catch(e) {}
                }

                log('Opening Focus Mode: ' + url);
                window.chrome.webview.postMessage(JSON.stringify({ type: 'openFocusMode', url: url }));
                
                e.preventDefault();
                e.stopPropagation();
            }
        }, true);
    })();
";

        /// <summary>
        /// メディア拡大自動化スクリプト
        /// </summary>
        public const string ScriptMediaExpand = @"
            (function() {
                // フラグ確認
                if (window.xColumnForceExpand !== true) return;

                function log(msg) {
                     try { window.chrome.webview.postMessage(JSON.stringify({ type: 'debugLog', message: '[MediaExpand] ' + msg })); } catch(e) {}
                }

                // URLからツイートIDを抽出
                const match = window.location.href.match(/\/status\/(\d+)/);
                const targetId = match ? match[1] : null;

                // 既にメディアURL(/photo/, /video/)の場合、Xが自動で開くのを待つのが基本
                // しかし読み込みタイミングによってはクリックが必要な場合もあるため、
                // IDが一致するツイートのみを対象にクリックを試行する
                
                log('Start. TargetID: ' + targetId);

                let attempts = 0;
                const maxAttempts = 50; 
                const interval = setInterval(() => {
                    attempts++;
                    if (attempts > maxAttempts) { clearInterval(interval); return; }

                    // モーダルが既に開いているかチェック
                    const modal = document.querySelector('div[role=""dialog""][aria-modal=""true""]');
                    if (modal && (modal.querySelector('img') || modal.querySelector('video'))) {
                        window.xColumnForceExpand = false;
                        clearInterval(interval);
                        log('Modal detected. Finished.');
                        return;
                    }

                    // ターゲットIDを持つツイートを探す
                    let targetArticle = null;
                    if (targetId) {
                        // 全ツイートを取得してIDチェック
                        const articles = document.querySelectorAll('article[data-testid=""tweet""]');
                        for (const art of articles) {
                            // タイムスタンプのリンクからIDを確認
                            const timeLink = art.querySelector('a[href*=""/status/""]');
                            if (timeLink && timeLink.href.includes(targetId)) {
                                targetArticle = art;
                                break;
                            }
                        }
                    } else {
                        // IDが取れない場合は最初のツイート（従来動作）
                         targetArticle = document.querySelector('article[data-testid=""tweet""]');
                    }

                    if (targetArticle) {
                        const targetMedia = targetArticle.querySelector('[data-testid=""tweetPhoto""]') || 
                                            targetArticle.querySelector('[data-testid=""videoPlayer""]') ||
                                            targetArticle.querySelector('[data-testid=""card.layoutLarge.media""]');
                
                        if (targetMedia) {
                            log('Clicking media in tweet ID: ' + targetId);
                            const clickEvent = new MouseEvent('click', { view: window, bubbles: true, cancelable: true });
                            targetMedia.dispatchEvent(clickEvent);
                            // クリックしたらループ終了（連打防止）
                            clearInterval(interval);
                            window.xColumnForceExpand = false;
                        }
                    }
                }, 200);
            })();
        ";

        /// <summary>
        /// サイドバーにある "/lists" を含むリンクを探してクリックします。
        /// </summary>
        public const string ScriptClickListButton = @"
            (function() {
                // 1. プロフィールリンクからユーザー名を抽出してURLを生成 (最も確実)
                // プロフィールボタンは極小ウィンドウでも表示され続けることが多いため
                const profileLink = document.querySelector('a[data-testid=""AppTabBar_Profile_Link""]');
                if (profileLink) {
                    const username = profileLink.getAttribute('href').replace('/', '');
                    // 正常なユーザー名（空でない、かつ 'home' でない）が取れた場合
                    if (username && username !== 'home' && username !== 'explore') {
                        window.location.replace('/' + username + '/lists');
                        return 'clicked';
                    }
                }

                // 2. サイドバーの「リスト」ボタンが直接見える場合はそれを使う
                const listLink = document.querySelector('a[data-testid=""AppTabBar_Lists_Link""]') || 
                                 document.querySelector('a[href$=""/lists""][role=""link""]');
                if (listLink) {
                    window.location.replace(listLink.href);
                    return 'clicked';
                }

                // 3. 左下のアカウントバッジからユーザー名を抽出
                const accountBadge = document.querySelector('[data-testid=""SideNav_AccountSwitcher_Badge""]');
                if (accountBadge) {
                    const match = accountBadge.innerText.match(/@(\w+)/);
                    if (match && match[1]) {
                        window.location.replace('/' + match[1] + '/lists');
                        return 'clicked';
                    }
                }

                return 'not_found';
            })();
        ";

        #endregion

        #region Helper Methods

        /// <summary>
        /// 音量を設定するスクリプトを生成します。
        /// </summary>
        public static string GetVolumeScript(double volume)
        {
            return $@"
                (function() {{
                    const vol = {volume};
                    document.querySelectorAll('video, audio').forEach(m => m.volume = vol);
                    if (!window.xColumnVolHook) {{
                        window.xColumnVolHook = true;
                        window.addEventListener('play', (e) => {{
                            if(e.target && (e.target.tagName === 'VIDEO' || e.target.tagName === 'AUDIO')) {{
                                e.target.volume = vol;
                            }}
                        }}, true);
                    }}
                }})();
            ";
        }

        /// <summary>
        /// CSSを注入するJavaScriptコードを生成します。
        /// </summary>
        public static string GetCssInjectionScript(string cssToInject)
        {
            if (string.IsNullOrEmpty(cssToInject)) return "";

            // エスケープ処理
            string safeCss = cssToInject.Replace("\\", "\\\\").Replace("`", "\\`").Replace("\r", "").Replace("\n", " ");

            return $@"
                (function() {{
                    let attempts = 0;
                    function injectXColumnStyle() {{
                        try {{
                            const head = document.head || document.getElementsByTagName('head')[0];
                            if (!head) {{
                                attempts++;
                                if (attempts < 10) setTimeout(injectXColumnStyle, 100);
                                return;
                            }}
                            let style = document.getElementById('xcolumn-custom-style');
                            if (!style) {{
                                style = document.createElement('style');
                                style.id = 'xcolumn-custom-style';
                                head.appendChild(style);
                            }}
                            style.textContent = `{safeCss}`;
                        }} catch(e) {{ console.error(e); }}
                    }}
                    injectXColumnStyle();
                }})();
            ";
        }

        /// <summary>
        /// NGワードを含むツイートを非表示にするスクリプトを生成します。
        /// </summary>
        public static string GetNgWordScript(List<string> ngWords)
        {
            if (ngWords == null || ngWords.Count == 0) return "";

            // JSの配列形式に変換
            string wordsJson = JsonSerializer.Serialize(ngWords);

            return $@"
                (function() {{
                    const ngWords = {wordsJson};
    
                    function checkAndHide(node) {{
                        // ツイートの本文テキストを取得 (data-testid='tweetText')
                        const tweetTextNode = node.querySelector('[data-testid=""tweetText""]');
                        if (!tweetTextNode) return;

                        const text = tweetTextNode.innerText.toLowerCase();
                        // いずれかのNGワードが含まれていれば非表示
                        const isNg = ngWords.some(word => text.includes(word.toLowerCase()));

                        if (isNg) {{
                            // nodeはcellInnerDivなので、その中身を非表示にする
                            node.style.display = 'none';
                            console.log('[XColumn] Hidden tweet containing NG word.');
                        }}
                    }}

                    // 既存のツイートをチェック
                    document.querySelectorAll('[data-testid=""cellInnerDiv""]').forEach(checkAndHide);

                    // 新しいツイートを監視
                    const observer = new MutationObserver((mutations) => {{
                        mutations.forEach((mutation) => {{
                            mutation.addedNodes.forEach((node) => {{
                                if (node.nodeType === 1 && node.querySelector) {{
                                    // 追加されたノード自体がセルか、内部にセルを持つか
                                    if (node.getAttribute('data-testid') === 'cellInnerDiv') {{
                                        checkAndHide(node);
                                    }} else {{
                                        node.querySelectorAll('[data-testid=""cellInnerDiv""]').forEach(checkAndHide);
                                    }}
                                }}
                            }});
                        }});
                    }});

                    observer.observe(document.body, {{ childList: true, subtree: true }});
                }})();
                ";
        }

        /// <summary>
        /// 動画の自動再生を強力にブロックするスクリプト。
        /// HTMLMediaElement.play() をフックし、直近のユーザー操作がない呼び出しを拒否します。
        /// </summary>
        public const string ScriptDisableVideoAutoplay = @"
            (function() {
                if (window.xColumnAutoplayBlocker) return;
                window.xColumnAutoplayBlocker = true;

                const originalPlay = HTMLMediaElement.prototype.play;
                let lastInteraction = 0;

                // ユーザーのクリックやキー操作の時刻を記録
                ['mousedown', 'touchstart', 'keydown', 'pointerdown'].forEach(evt => {
                    document.addEventListener(evt, () => {
                        lastInteraction = Date.now();
                    }, { capture: true, passive: true });
                });

                // play()メソッドを上書き
                HTMLMediaElement.prototype.play = function() {
                    const now = Date.now();
                    // ユーザー操作から 300ms 以内の呼び出しなら許可（手動再生）
                    // それ以外（スクロール検知などによる自動再生）はブロック
                    if (now - lastInteraction < 300) {
                        return originalPlay.apply(this, arguments);
                    } else {
                        // コンソールにログを残して拒否
                        // console.log('[XColumn] Autoplay blocked:', this);
                        return Promise.reject(new DOMException('Autoplay blocked by XColumn', 'NotAllowedError'));
                    }
                };
            })();
        ";

        /// <summary>
        /// 時刻表示を相対時刻から絶対時刻(yyyy/MM/dd HH:mm)に変換するスクリプト。
        /// 即時反映と定期実行、およびOFF時の復元に対応。
        /// </summary>
        public const string ScriptAbsoluteTime = @"
            (function() {
                // メインロジック関数を定義
                window.xColumnUpdateAbsTime = function() {
                    const isEnabled = window.xColumnShowAbsoluteTime;

                    function formatTime(isoStr) {
                        const date = new Date(isoStr);
                        if (isNaN(date.getTime())) return null;
                        
                        const year = date.getFullYear();
                        const month = (date.getMonth() + 1).toString().padStart(2, '0');
                        const day = date.getDate().toString().padStart(2, '0');
                        const hours = date.getHours().toString().padStart(2, '0');
                        const minutes = date.getMinutes().toString().padStart(2, '0');
                        
                        return `${year}/${month}/${day} ${hours}:${minutes}`;
                    }

                    document.querySelectorAll('time').forEach(t => {
                        // 【OFFの場合】: 元に戻す処理
                        if (!isEnabled) {
                            // 退避しておいたテキストがあれば戻す
                            if (t.hasAttribute('data-original-text')) {
                                t.innerText = t.getAttribute('data-original-text');
                                t.removeAttribute('data-original-text');
                            }
                            return;
                        }

                        // 【ONの場合】: 絶対時刻に書き換える処理
                        const dt = t.getAttribute('datetime');
                        if (!dt) return;
                        
                        const absText = formatTime(dt);
                        if (!absText) return;

                        // まだ退避していなければ、現在の表示(相対時刻)を属性にバックアップ
                        // (ただし、既に絶対時刻になっている場合はバックアップしない)
                        if (!t.hasAttribute('data-original-text')) {
                            if (t.innerText !== absText) {
                                t.setAttribute('data-original-text', t.innerText);
                            }
                        }

                        // 表示を書き換え
                        if (t.innerText !== absText) {
                             t.innerText = absText;
                        }
                    });
                };

                // 初回注入時のみタイマーをセット
                if (!window.xColumnAbsTimeInit) {
                    window.xColumnAbsTimeInit = true;
                    setInterval(window.xColumnUpdateAbsTime, 1000);
                }

                // 注入直後にも一度実行する
                window.xColumnUpdateAbsTime();
            })();
        ";
    }
    #endregion
}