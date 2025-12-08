using System;

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
        /// リプライ検出スクリプト
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

        #endregion
    }
}