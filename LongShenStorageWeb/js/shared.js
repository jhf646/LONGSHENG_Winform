// ===== 氢晨库存管理系统 - 公共模块 =====
const API_BASE = 'http://localhost:5000/api';

// 认证信息
let authToken = localStorage.getItem('ls_token') || '';
let currentUser = null;
try { currentUser = JSON.parse(localStorage.getItem('ls_user') || 'null'); } catch(e) { localStorage.removeItem('ls_user'); }

// 未登录跳转
if (!authToken || !currentUser) {
  window.location.href = 'login.html';
}

// 内置超级管理员ID
const BUILT_IN_USER_ID = '10000000-0000-0000-0000-000000000001';

// ===== API 请求 =====
async function api(path, options = {}) {
    const headers = { 'Content-Type': 'application/json' };
    if (authToken) headers['Authorization'] = `Bearer ${authToken}`;
    const resp = await fetch(`${API_BASE}${path}`, { headers: { ...headers, ...options.headers }, ...options });
    if (resp.status === 401) { logout(); throw new Error('登录已过期'); }
    if (!resp.ok) { const err = await resp.json().catch(() => ({ error: resp.statusText })); throw new Error(err.error || `HTTP ${resp.status}`); }
    return resp.json();
}

// ===== 实用工具 =====
function toast(msg, type = '') {
    const t = document.createElement('div'); t.className = `toast ${type}`; t.textContent = msg;
    document.body.appendChild(t); setTimeout(() => t.remove(), 3000);
}
function formatTime(d) { return new Date(d).toLocaleString('zh-CN', { hour12: false }); }
function hasRole(...roles) { return currentUser && roles.includes(currentUser.role); }

// ===== 登出 =====
function logout() {
    authToken = ''; currentUser = null;
    localStorage.removeItem('ls_token'); localStorage.removeItem('ls_user');
    window.location.href = 'login.html';
}

// ===== 页面初始化 =====
function initPage(pageId, pageTitle) {
    // 显示侧边栏和主内容
    document.querySelector('.sidebar').style.display = 'flex';
    document.querySelector('.main').style.display = 'block';

    // 设置页面标题
    if (pageTitle) document.getElementById('pageTitle').textContent = pageTitle;

    // 高亮当前导航
    document.querySelectorAll('.nav-item').forEach(n => n.classList.toggle('active', n.dataset.page === pageId));

    // 更新用户信息
    document.getElementById('sidebarUserName').textContent = currentUser.displayName;
    const roleMap = { Admin: '管理员', Operator: '操作员', Viewer: '查看员' };
    document.getElementById('sidebarUserRole').textContent = roleMap[currentUser.role] || currentUser.role;

    // 应用权限
    applyPagePermissions();

    // 启动时钟
    updateClock();
    setInterval(updateClock, 1000);
}

function updateClock() {
    const now = new Date();
    const el = document.getElementById('topbarClock');
    if (el) el.textContent = now.toLocaleString('zh-CN', { hour12: false, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function applyPagePermissions() {
    const isAdmin = hasRole('Admin');
    const canWrite = hasRole('Admin', 'Operator');
    document.querySelectorAll('.perm-admin').forEach(el => el.classList.toggle('hidden', !isAdmin));
    document.querySelectorAll('.perm-write').forEach(el => el.classList.toggle('hidden', !canWrite));
    if (currentUser && currentUser.allowedPages && currentUser.allowedPages.length > 0) {
        document.querySelectorAll('.nav-item').forEach(el => {
            const page = el.dataset.page;
            if (page && !currentUser.allowedPages.includes(page)) el.classList.add('hidden');
        });
    }
}

// 托盘号格式化
function formatPalletInput(el) {
    const digits = el.value.replace(/\D/g, '');
    if (digits.length > 0 && digits.length <= 3) { el.value = digits.padStart(3, '0'); }
    else if (digits.length > 3) { el.value = digits.slice(-3).padStart(3, '0'); }
}
