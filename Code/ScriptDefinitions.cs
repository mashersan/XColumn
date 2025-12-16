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
        /// ユーザー操作（ホイール、タッチ等）を検知した場合は、即座に復元処理を中断してガクつきを防ぎます。
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

                // スクロール位置を保存
                let saveTimer;
                window.addEventListener('scroll', () => {
                    clearTimeout(saveTimer);
                    saveTimer = setTimeout(() => {
                        const y = window.scrollY;
                        if (y > 0) {
                            sessionStorage.setItem(getKey(), y);
                        }
                    }, 200);
                }, { passive: true });

                let restoreTimers = [];

                function cancelRestoration() {
                    if (restoreTimers.length > 0) {
                        restoreTimers.forEach(id => clearTimeout(id));
                        restoreTimers = [];
                    }
                }

                // ユーザー操作検知で復元中止
                ['wheel', 'touchmove', 'keydown', 'mousedown'].forEach(evt => {
                    window.addEventListener(evt, cancelRestoration, { passive: true, capture: true });
                });

                function restorePosition() {
                    cancelRestoration();
                    const key = getKey();
                    const savedY = sessionStorage.getItem(key);
                    
                    if (savedY) {
                        const targetY = parseInt(savedY, 10);
                        if (!isNaN(targetY) && targetY > 0) {
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
        /// メディア拡大自動化スクリプト
        /// </summary>
        public const string ScriptMediaExpand = @"
            (function() {
                const url = window.location.href;
                if (!url.includes('/photo/') && !url.includes('/video/')) return;
                const idMatch = url.match(/\/status\/(\d+)/);
                const targetId = idMatch ? idMatch[1] : null;
                let attempts = 0;
                const maxAttempts = 20;
                const interval = setInterval(() => {
                    attempts++;
                    if (attempts > maxAttempts) { clearInterval(interval); return; }
                    if (document.querySelector('div[role=""dialog""][aria-modal=""true""]')) {
                        clearInterval(interval);
                        return;
                    }
                    let targetTweet = null;
                    const tweets = document.querySelectorAll('article[data-testid=""tweet""]');
                    if (targetId) {
                        for (const t of tweets) {
                            if (t.innerHTML.indexOf(targetId) !== -1) {
                                targetTweet = t;
                                break;
                            }
                        }
                    }
                    if (!targetTweet && tweets.length > 0) targetTweet = tweets[0];
                    if (targetTweet) {
                        if (url.includes('/photo/')) {
                            const photoMatch = url.match(/\/photo\/(\d+)/);
                            if (photoMatch) {
                                const index = parseInt(photoMatch[1]) - 1;
                                const photos = targetTweet.querySelectorAll('div[data-testid=""tweetPhoto""]');
                                if (photos[index]) {
                                    photos[index].click();
                                    clearInterval(interval);
                                }
                            }
                        }
                        else if (url.includes('/video/')) {
                            const video = targetTweet.querySelector('div[data-testid=""videoPlayer""]');
                            if (video) {
                                video.click();
                                clearInterval(interval);
                            }
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
                const listLink = document.querySelector('a[href$=""/lists""][role=""link""]');
                if (listLink) {
                    // href属性（遷移先URL）を取得して、現在のページを置き換える
                    window.location.replace(listLink.href);
                    return 'clicked';
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


    }

        #endregion
}
