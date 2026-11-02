// ===== 氢晨库存管理系统 - 应用外壳（单页多标签，页面以 iframe 保活） =====
(function () {
    const STORAGE_KEY = 'ls_open_tabs';
    const STATUS_INTERVAL = 15000;

    const navItems = Array.prototype.slice.call(document.querySelectorAll('#sidebarNav .nav-item[data-page]'));
    const pageMeta = {};
    navItems.forEach(function (a) {
        pageMeta[a.dataset.page] = {
            href: a.getAttribute('href'),
            title: a.dataset.title || a.textContent.trim(),
            icon: a.querySelector('.nav-icon') ? a.querySelector('.nav-icon').textContent : ''
        };
    });

    const tabbar = document.getElementById('tabbar');
    const frameWrap = document.getElementById('frameWrap');
    const tabs = new Map();   // pageId -> { tab, frame, title }
    let activePage = null;

    // ===== 页签 =====
    function isAllowed(pageId) {
        const link = document.querySelector('#sidebarNav .nav-item[data-page="' + pageId + '"]');
        return !!link && !link.classList.contains('hidden');
    }

    function firstAllowedPage() {
        for (let i = 0; i < navItems.length; i++) {
            if (!navItems[i].classList.contains('hidden')) return navItems[i].dataset.page;
        }
        return navItems.length ? navItems[0].dataset.page : null;
    }

    function createTab(pageId) {
        if (tabs.has(pageId) || !pageMeta[pageId]) return tabs.get(pageId);
        const meta = pageMeta[pageId];

        const frame = document.createElement('iframe');
        frame.className = 'page-frame';
        frame.title = meta.title;
        frame.src = meta.href;
        frameWrap.appendChild(frame);

        const tab = document.createElement('div');
        tab.className = 'app-tab';
        tab.dataset.page = pageId;
        tab.title = meta.title;

        const icon = document.createElement('span');
        icon.className = 'app-tab-icon';
        icon.textContent = meta.icon;
        const label = document.createElement('span');
        label.className = 'app-tab-label';
        label.textContent = meta.title;
        const close = document.createElement('span');
        close.className = 'app-tab-close';
        close.textContent = '✕';
        close.title = '关闭';
        tab.appendChild(icon);
        tab.appendChild(label);
        tab.appendChild(close);

        tab.addEventListener('click', function (e) {
            if (e.target === close) closeTab(pageId);
            else activateTab(pageId);
        });
        tab.addEventListener('auxclick', function (e) {
            if (e.button === 1) { e.preventDefault(); closeTab(pageId); }
        });

        tabbar.appendChild(tab);
        const entry = { tab: tab, frame: frame, title: meta.title, visited: false };
        tabs.set(pageId, entry);

        // 子页面加载完成后同步标题（同源时可用，跨域/文件协议下忽略）
        frame.addEventListener('load', function () { syncTitle(pageId); });
        return entry;
    }

    function syncTitle(pageId) {
        const entry = tabs.get(pageId);
        if (!entry) return;
        let title = '';
        try {
            title = (entry.frame.contentDocument && entry.frame.contentDocument.title) || '';
        } catch (e) {
            return;
        }
        title = title.split(' - ').pop().trim();
        if (!title || title === entry.title) return;
        entry.title = title;
        entry.tab.querySelector('.app-tab-label').textContent = title;
        entry.tab.title = title;
        if (activePage === pageId) setTopbarTitle(title);
    }

    function activateTab(pageId) {
        const entry = tabs.get(pageId);
        if (!entry) return;
        const switched = activePage !== pageId;
        activePage = pageId;
        tabs.forEach(function (e, id) {
            e.tab.classList.toggle('active', id === pageId);
            e.frame.classList.toggle('active', id === pageId);
        });
        navItems.forEach(function (a) { a.classList.toggle('active', a.dataset.page === pageId); });
        setTopbarTitle(entry.title);
        if (entry.tab.scrollIntoView) {
            try { entry.tab.scrollIntoView({ block: 'nearest', inline: 'nearest' }); } catch (e) { /* 忽略 */ }
        }
        // 首次显示由页面自身初始化，之后每次切回通知页面刷新数据（保留表单内容）
        if (entry.visited && switched) notifyActivate(entry.frame);
        entry.visited = true;
        saveState();
    }

    function notifyActivate(frame) {
        try {
            const win = frame.contentWindow;
            if (win && typeof win.runPageActivate === 'function') win.runPageActivate();
        } catch (e) { /* 页面加载中或跨域，忽略 */ }
    }

    // 重新加载当前页签（页签栏 🔄 按钮）
    function refreshActivePage() {
        const entry = tabs.get(activePage);
        if (!entry) return;
        try {
            entry.frame.contentWindow.location.reload();
        } catch (e) {
            entry.frame.src = entry.frame.src;
        }
    }

    window.refreshActivePage = refreshActivePage;

    function closeTab(pageId) {
        const entry = tabs.get(pageId);
        if (!entry) return;
        const ids = Array.from(tabs.keys());
        const idx = ids.indexOf(pageId);

        entry.tab.remove();
        entry.frame.remove();
        tabs.delete(pageId);

        if (activePage === pageId) {
            activePage = null;
            const remain = Array.from(tabs.keys());
            if (remain.length) {
                activateTab(remain[Math.min(idx, remain.length - 1)]);
            } else {
                const fallback = firstAllowedPage();
                if (fallback) openPage(fallback);
            }
        } else {
            saveState();
        }
    }

    function openPage(pageId) {
        if (!pageMeta[pageId]) return;
        createTab(pageId);
        activateTab(pageId);
    }

    function setTopbarTitle(title) {
        const el = document.getElementById('pageTitle');
        if (el) el.textContent = title;
    }

    // 刷新后恢复本浏览器标签页打开的页签
    function saveState() {
        try {
            sessionStorage.setItem(STORAGE_KEY, JSON.stringify({
                tabs: Array.from(tabs.keys()),
                active: activePage
            }));
        } catch (e) { /* 忽略存储异常 */ }
    }

    function restoreTabs() {
        let state = null;
        try { state = JSON.parse(sessionStorage.getItem(STORAGE_KEY) || 'null'); } catch (e) { state = null; }

        const ids = [];
        if (state && Array.isArray(state.tabs)) {
            state.tabs.forEach(function (id) {
                if (pageMeta[id] && isAllowed(id) && ids.indexOf(id) < 0) ids.push(id);
            });
        }
        if (!ids.length) {
            const first = firstAllowedPage();
            if (first) ids.push(first);
        }
        ids.forEach(createTab);
        const target = state && state.active && tabs.has(state.active) ? state.active : ids[0];
        if (target) activateTab(target);
    }

    // ===== 初始化 =====
    function initShell() {
        if (!authToken || !currentUser) return;   // 未登录时 shared.js 已跳转登录页

        const avatarEl = document.getElementById('sidebarAvatar');
        const nameEl = document.getElementById('sidebarUserName');
        const roleEl = document.getElementById('sidebarUserRole');
        const roleMap = { Admin: '管理员', Operator: '操作员', Viewer: '查看员' };
        const displayName = currentUser.displayName || currentUser.username || '-';
        if (nameEl) nameEl.textContent = displayName;
        if (avatarEl) avatarEl.textContent = displayName.trim().charAt(0).toUpperCase();
        if (roleEl) roleEl.textContent = roleMap[currentUser.role] || currentUser.role || '-';

        applyPagePermissions();

        updateClock();
        setInterval(updateClock, 1000);
        updateHeaderStatus();
        setInterval(updateHeaderStatus, STATUS_INTERVAL);

        navItems.forEach(function (a) {
            a.addEventListener('click', function (e) {
                if (e.ctrlKey || e.metaKey || e.shiftKey) return;   // 保留新窗口打开的浏览器行为
                e.preventDefault();
                openPage(a.dataset.page);
            });
        });

        restoreTabs();
    }

    initShell();
})();
