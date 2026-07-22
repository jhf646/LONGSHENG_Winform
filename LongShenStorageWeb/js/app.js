// ===== 氢晨库存管理系统 Web v3 =====
const API_BASE = 'http://localhost:5000/api';
let authToken = localStorage.getItem('ls_token') || '';
let currentUser = JSON.parse(localStorage.getItem('ls_user') || 'null');

// 内置超级管理员账号1001的固定ID（不可删除/修改）
const BUILT_IN_USER_ID = '10000000-0000-0000-0000-000000000001';

// ===== 工具 =====
async function api(path, options = {}) {
    const headers = { 'Content-Type': 'application/json' };
    if (authToken) headers['Authorization'] = `Bearer ${authToken}`;
    const resp = await fetch(`${API_BASE}${path}`, { headers: { ...headers, ...options.headers }, ...options });
    if (resp.status === 401) { logout(); throw new Error('登录已过期'); }
    if (!resp.ok) { const err = await resp.json().catch(() => ({ error: resp.statusText })); throw new Error(err.error || `HTTP ${resp.status}`); }
    return resp.json();
}
function toast(msg, type = '') {
    const t = document.createElement('div'); t.className = `toast ${type}`; t.textContent = msg;
    document.body.appendChild(t); setTimeout(() => t.remove(), 3000);
}
function formatTime(d) { return new Date(d).toLocaleString('zh-CN', { hour12: false }); }
function hasRole(...roles) { return currentUser && roles.includes(currentUser.role); }

// ===== 登录/登出 =====
function showLogin() {
    document.getElementById('loginPage').style.display = 'flex';
    document.querySelector('.sidebar').style.display = 'none';
    document.querySelector('.main').style.display = 'none';
}
function hideLogin() {
    document.getElementById('loginPage').style.display = 'none';
    document.querySelector('.sidebar').style.display = 'flex';
    document.querySelector('.main').style.display = 'block';
}
function logout() {
    authToken = ''; currentUser = null;
    localStorage.removeItem('ls_token'); localStorage.removeItem('ls_user');
    showLogin(); document.getElementById('sidebarUserName').textContent = '未登录';
}

async function handleLogin() {
    const username = document.getElementById('loginUser').value.trim();
    const password = document.getElementById('loginPass').value;
    if (!username || !password) { toast('请输入用户名和密码', 'error'); return; }
    try {
        const resp = await api('/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) });
        authToken = resp.token; currentUser = resp;
        localStorage.setItem('ls_token', resp.token);
        localStorage.setItem('ls_user', JSON.stringify(resp));
        hideLogin();
        updateUserUI();
        applyPagePermissions();
        await initApp();
        toast(`欢迎, ${resp.displayName}`, 'success');
    } catch (e) { toast('登录失败: ' + e.message, 'error'); }
}

function updateUserUI() {
    document.getElementById('sidebarAvatar').innerHTML = '👤';
    document.getElementById('sidebarUserName').textContent = currentUser.displayName;
    const roleMap = { Admin: '管理员', Operator: '操作员', Viewer: '查看员' };
    document.getElementById('sidebarUserRole').textContent = roleMap[currentUser.role] || currentUser.role;
}

function applyPagePermissions() {
    const isAdmin = hasRole('Admin');
    const canWrite = hasRole('Admin', 'Operator');
    document.querySelectorAll('.perm-admin').forEach(el => el.classList.toggle('hidden', !isAdmin));
    document.querySelectorAll('.perm-write').forEach(el => el.classList.toggle('hidden', !canWrite));
    // 根据角色权限动态控制菜单显示
    if (currentUser && currentUser.allowedPages && currentUser.allowedPages.length > 0) {
        document.querySelectorAll('.nav-item').forEach(el => {
            const page = el.dataset.page;
            if (page && !currentUser.allowedPages.includes(page)) {
                el.classList.add('hidden');
            }
        });
    }
}

// ===== 页面导航 =====
async function switchPage(pageId) {
    // 检查页面权限
    if (currentUser && currentUser.allowedPages && currentUser.allowedPages.length > 0 && !currentUser.allowedPages.includes(pageId)) {
        toast('无权限访问此页面', 'error');
        switchPage('dashboard');
        return;
    }
    document.querySelectorAll('.nav-item').forEach(n => n.classList.toggle('active', n.dataset.page === pageId));
    document.querySelectorAll('.page').forEach(p => p.classList.toggle('active', p.id === `page-${pageId}`));
    const titles = { dashboard: '仪表盘', inbound: '入库管理', outbound: '出库管理', slots: '货位管理', ledger: '台账查询', report: '报表统计', users: '用户管理', alert: '库存预警', roles: '角色权限', devmonitor: '设备监控', devcontrol: '设备调用' };
    document.getElementById('pageTitle').textContent = titles[pageId] || pageId;
    try {
        if (pageId === 'dashboard') { loadDashboard(); }
        else if (pageId === 'slots') { loadSlotPage(); }
        else if (pageId === 'ledger') { resetLedger(); queryLedger(); }
        else if (pageId === 'users') { loadUserList(); }
        else if (pageId === 'alert') { loadAlertPage(); }
        else if (pageId === 'roles') { loadRolePage(); }
        else if (pageId === 'devcontrol') { await loadDeviceRegisters(); }
        else if (pageId === 'devmonitor') { await loadDeviceMonitor(); }
    } catch(e) { console.warn('页面加载错误:', e); }
}

document.getElementById('tabNav').addEventListener('click', e => {
    e.preventDefault();
    const item = e.target.closest('.nav-item');
    if (item && !item.classList.contains('hidden')) switchPage(item.dataset.page);
});

// ===== 初始化 =====
function updateClock() {
    const now = new Date();
    document.getElementById('topbarClock').textContent = now.toLocaleString('zh-CN', { hour12: false, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' });
}
async function initApp() {
    await Promise.all([loadDashboard(), loadDropdowns()]);
    loadOutboundOptions();
    const now = new Date();
    document.getElementById('ledgerStart').value = new Date(now.getTime() - 7*86400000).toISOString().slice(0,16);
    document.getElementById('ledgerEnd').value = now.toISOString().slice(0,16);
    loadInboundRecent(); loadOutboundRecent();
}

document.addEventListener('DOMContentLoaded', () => {
    if (authToken && currentUser) {
        hideLogin(); updateUserUI(); applyPagePermissions(); initApp();
    } else {
        showLogin();
    }
    // 启动时钟
    updateClock();
    setInterval(updateClock, 1000);
});

// ===== 下拉选项 =====
async function loadDropdowns() {
    try {
        const data = await api('/appstate/dropdowns');
        ['dlPallet','dlTooling','dlProject','dlModel','dlCustomer'].forEach((id, i) => {
            const keys = ['palletNumbers','toolingNumbers','projectNumbers','modelTypes','customerNames'];
            document.getElementById(id).innerHTML = (data[keys[i]] || []).map(v => `<option value="${v}">`).join('');
        });
    } catch (e) { console.warn('加载下拉选项失败:', e); }
}

// ===== 仪表盘 =====
async function loadDashboard() {
    try {
        const data = await api('/appstate/dashboard');
        document.getElementById('metricCount').textContent = data.inventoryCount;
        document.getElementById('metricOccupied').textContent = data.occupiedSlots;
        document.getElementById('metricFree').textContent = data.freeSlots;
        const alertEl = document.getElementById('metricAlert');
        alertEl.textContent = data.alertStatus;
        alertEl.style.color = data.isAlert ? '#d92d20' : '#1d2939';
        document.getElementById('headerStatus').textContent = `📊 ${data.inventoryCount}件 · 🟠${data.occupiedSlots}占用 · 🟢${data.freeSlots}空闲 · ${data.isAlert ? '⚠️' : '✅'}${data.alertStatus}`;
        renderSlotVisual('dashboardSlots', data.slots);
        const tbody = document.getElementById('recentBody');
        tbody.innerHTML = (data.recentInventory || []).slice(0,10).map(r =>
            `<tr><td>${r.palletNumber}</td><td>${r.toolingNumber}</td><td>${r.slotCode}</td><td>${r.lastOperator}</td><td>${formatTime(r.inboundTime)}</td></tr>`
        ).join('') || '<tr><td colspan="5" class="empty-state">暂无数据</td></tr>';
    } catch (e) { toast('加载失败: ' + e.message, 'error'); }
}

// ===== 货位可视化 =====
function renderSlotVisual(containerId, slots) {
    const container = document.getElementById(containerId);
    if (!container || !slots) return;
    container.innerHTML = '';
    for (let row = 1; row <= 2; row++) {
        const rack = document.createElement('div'); rack.className = 'slot-rack';
        const hdr = document.createElement('h4'); hdr.textContent = `第 ${row} 排`; rack.appendChild(hdr);
        const grid = document.createElement('div'); grid.className = 'rack-grid';
        const c = document.createElement('div'); c.className = 'header-cell'; c.textContent = '层/列'; grid.appendChild(c);
        for (let c2 = 4; c2 >= 1; c2--) { const hc = document.createElement('div'); hc.className = 'header-cell'; hc.textContent = `${c2}列`; grid.appendChild(hc); }
        for (let lv = 8; lv >= 1; lv--) {
            const lh = document.createElement('div'); lh.className = 'header-cell'; lh.textContent = `${lv}层`; grid.appendChild(lh);
            for (let col = 4; col >= 1; col--) {
                const slot = slots.find(s => s.rowNumber === row && s.columnNumber === col && s.levelNumber === lv);
                const cell = document.createElement('div');
                cell.className = `cell ${slot?.isEnabled === false ? 'disabled' : (slot?.isOccupied ? 'occupied' : 'free')}`;
                if (slot) {
                    const isEntrance = slot.rowNumber === 1 && slot.columnNumber === 1 && slot.levelNumber === 1;
                    const isDisabled = slot.isEnabled === false;
                    cell.innerHTML = isEntrance
                        ? `<div class="slot-status" style="font-size:9px;color:#667085">🚪总出入口</div>`
                        : isDisabled
                        ? `<div class="slot-status" style="font-size:9px;color:#999">⚪禁用</div>`
                        : `<div class="slot-status">${slot.isOccupied ? '🔴占用' : `🟢空闲 ${String(slot.internalNumber).padStart(3, '0')}`}</div>`;
                    cell.title = `${slot.slotCode} (内部编号: ${String(slot.internalNumber).padStart(3, '0')})${isDisabled ? ' [已禁用]' : ''}`;
                    cell.onclick = () => handleSlotClick(slot);
                }
                grid.appendChild(cell);
            }
        }
        rack.appendChild(grid); container.appendChild(rack);
    }
}

// ===== 入库 =====
let lastWorkOrder = '', lastCellNumber = '';
function clearInbound() {
    ['inPallet','inTooling','inProject','inModel','inCustomer','inOperator','inSlot','inNotes'].forEach(id => {
        const el = document.getElementById(id); if (el) el.value = '';
    });
    document.getElementById('inComponentSections').value = '1';
}

async function handleInbound(useSpecifiedSlot) {
    const operator = document.getElementById('inOperator').value.trim();
    if (!operator) { toast('请输入操作人员', 'error'); return; }
    const payload = {
        palletNumber: document.getElementById('inPallet').value.trim(),
        toolingNumber: document.getElementById('inTooling').value.trim(),
        projectNumber: document.getElementById('inProject').value.trim(),
        modelType: document.getElementById('inModel').value.trim(),
        workOrder: document.getElementById('inWorkOrder').value.trim(),
        cellNumber: document.getElementById('inCellNumber').value.trim(),
        componentSections: parseInt(document.getElementById('inComponentSections').value) || 1,
        customerName: document.getElementById('inCustomer').value.trim(),
        operatorName: operator,
        specifiedSlot: useSpecifiedSlot ? document.getElementById('inSlot').value.trim() : null,
        notes: document.getElementById('inNotes').value.trim()
    };
    try {
        await api('/workpiecerecords/inbound', { method: 'POST', body: JSON.stringify(payload) });
        toast(`✅ ${payload.palletNumber || '?'} 入库成功`, 'success');
        lastWorkOrder = payload.workOrder; lastCellNumber = payload.cellNumber;
        clearInbound();
        document.getElementById('inWorkOrder').value = lastWorkOrder;
        document.getElementById('inCellNumber').value = lastCellNumber;
        const pallet = document.getElementById('inPallet').value.trim();
        const tooling = document.getElementById('inTooling').value.trim();
        const project = document.getElementById('inProject').value.trim();
        const model = document.getElementById('inModel').value.trim();
        const customer = document.getElementById('inCustomer').value.trim();
        try { await api('/appstate/dropdowns', { method: 'POST', body: JSON.stringify({
            palletNumbers: pallet ? [pallet] : [], toolingNumbers: tooling ? [tooling] : [],
            projectNumbers: project ? [project] : [], modelTypes: model ? [model] : [],
            customerNames: customer ? [customer] : []
        }) }); } catch(e) {}
        await Promise.all([loadDashboard(), loadDropdowns()]);
        loadInboundRecent();
    } catch (e) { toast('❌ 入库失败: ' + e.message, 'error'); }
}

async function loadInboundRecent() {
    try {
        const entries = await api('/ledgerentries');
        document.getElementById('inboundRecentBody').innerHTML = entries.slice(0,15).map(e =>
            `<tr><td>${formatTime(e.timestamp)}</td><td>${e.palletNumber}</td><td>${e.operatorName}</td><td>${e.slotCode}</td></tr>`
        ).join('') || '<tr><td colspan="4" class="empty-state">暂无数据</td></tr>';
    } catch(e) {}
}

// ===== 出库 =====
async function loadOutboundOptions() {
    try {
        const items = await api('/workpiecerecords');
        document.getElementById('outRecord').innerHTML = items.map(r =>
            `<option value="${r.id}">${r.palletNumber} | ${r.modelType || '-'} | ${r.slotCode}</option>`
        ).join('') || '<option value="">暂无在库工件</option>';
    } catch(e) { toast('加载失败', 'error'); }
}

async function handleOutbound() {
    const recordId = document.getElementById('outRecord').value;
    const operator = document.getElementById('outOperator').value.trim();
    if (!recordId || !operator) { toast('请选择工件并输入操作人员', 'error'); return; }
    try {
        await api('/workpiecerecords/outbound', { method: 'POST', body: JSON.stringify({
            recordId, operatorName: operator,
            specifiedSlot: document.getElementById('outSlot').value.trim() || null
        })});
        toast('✅ 出库成功', 'success');
        document.getElementById('outOperator').value = ''; document.getElementById('outSlot').value = '';
        await Promise.all([loadDashboard(), loadOutboundOptions()]);
        loadOutboundRecent();
    } catch(e) { toast('❌ 出库失败: ' + e.message, 'error'); }
}

async function loadOutboundRecent() {
    try {
        const entries = await api('/ledgerentries');
        document.getElementById('outboundBody').innerHTML = entries.filter(e => e.type === 1).slice(0,15).map(e =>
            `<tr><td>${formatTime(e.timestamp)}</td><td>${e.palletNumber}</td><td>${e.operatorName}</td><td>${e.actionDescription}</td></tr>`
        ).join('') || '<tr><td colspan="4" class="empty-state">暂无数据</td></tr>';
    } catch(e) {}
}

// ===== 货位 =====
let selectedSlotCode = null;
let editingSlotData = null;

function handleSlotClick(slot) {
    selectedSlotCode = slot.slotCode;
    showSlotDetail(slot);
}
async function showSlotDetail(slot) {
    try {
        const items = await api('/workpiecerecords');
        const wp = items.find(i => i.id === slot.workpieceId);
        const isFree = !slot.isOccupied;
        const isDisabled = slot.isEnabled === false;
        document.getElementById('slotDetailBody').innerHTML = `
            <div style="text-align:center;margin-bottom:20px">
                <div style="font-size:40px;margin-bottom:8px">${isDisabled ? '⚪' : (isFree ? '🟢' : '🔴')}</div>
                <div style="font-size:18px;font-weight:bold;color:#1d2939">${slot.slotCode}</div>
                <div style="font-size:13px;color:#98a2b3;margin-top:2px">${slot.zone} · ${slot.rowNumber}排${slot.columnNumber}列${slot.levelNumber}层 · 内部编号: ${String(slot.internalNumber).padStart(3, '0')} ${isDisabled ? '· ⚪已禁用' : ''}</div>
            </div>
            <div style="border-top:1px solid #e8ecf0;padding-top:16px">
                <div style="display:grid;grid-template-columns:1fr 1fr;gap:10px 20px;font-size:13px">
                    ${wp ? `
                    <div><span style="color:#98a2b3">托盘号</span><br><span style="font-weight:600">${wp.palletNumber || '-'}</span></div>
                    <div><span style="color:#98a2b3">工装号</span><br><span style="font-weight:600">${wp.toolingNumber || '-'}</span></div>
                    <div><span style="color:#98a2b3">项目号</span><br><span style="font-weight:600">${wp.projectNumber || '-'}</span></div>
                    <div><span style="color:#98a2b3">型号</span><br><span style="font-weight:600">${wp.modelType || '-'}</span></div>
                    <div><span style="color:#98a2b3">工单号</span><br><span style="font-weight:600">${wp.workOrder || '-'}</span></div>
                    <div><span style="color:#98a2b3">电解槽编号</span><br><span style="font-weight:600">${wp.cellNumber || '-'}</span></div>
                    <div><span style="color:#98a2b3">组件节数</span><br><span style="font-weight:600">${wp.componentSections || '-'}</span></div>
                    <div><span style="color:#98a2b3">客户名称</span><br><span style="font-weight:600">${wp.customerName || '-'}</span></div>
                    <div style="grid-column:1/-1"><span style="color:#98a2b3">操作人员</span><br><span style="font-weight:600">${wp.lastOperator || '-'}</span></div>
                    <div style="grid-column:1/-1"><span style="color:#98a2b3">入库时间</span><br><span style="font-weight:600">${formatTime(wp.inboundTime)}</span></div>
                    <div style="grid-column:1/-1;margin-bottom:8px"><span style="color:#98a2b3">备注</span><br><span style="font-weight:600">${wp.notes || '-'}</span></div>
                    ` : '<div style="grid-column:1/-1;text-align:center;color:#12b76a;padding:16px 0;font-size:15px">✅ 该库位空闲</div>'}
                </div>
            </div>
            <div style="border-top:1px solid #e8ecf0;padding-top:14px;margin-top:4px;text-align:center;display:flex;gap:8px;justify-content:center">
                <button class="btn" onclick="closeSlotDetail()">关闭</button>
                <button class="btn btn-primary" onclick="closeSlotDetail();openSlotEdit('${slot.slotCode}')">✏️ 编辑库位</button>
            </div>
        `;
        document.getElementById('slotDetailModal').classList.remove('hidden');
    } catch(e) {
        toast('加载失败: ' + e.message, 'error');
    }
}
function closeSlotDetail() {
    document.getElementById('slotDetailModal').classList.add('hidden');
}

// ----- 搜索过滤 -----
function filterSlotTable() {
    const keyword = (document.getElementById('slotSearch').value || '').toLowerCase().trim();
    const statusFilter = document.getElementById('slotFilterStatus').value;
    const rows = document.querySelectorAll('#slotTableBody tr');
    rows.forEach(tr => {
        const text = tr.textContent.toLowerCase();
        const status = tr.dataset.status || '';
        const matchKeyword = !keyword || text.includes(keyword);
        const matchStatus = !statusFilter || status === statusFilter;
        tr.style.display = matchKeyword && matchStatus ? '' : 'none';
    });
}

// ----- 编辑库位 -----
function openSlotEdit(slotCode) {
    selectedSlotCode = slotCode;
    // 从加载的数据中查找
    if (!editingSlotData) return;
    const slot = editingSlotData.find(s => s.slotCode === slotCode);
    if (!slot) { toast('未找到库位数据', 'error'); return; }
    
    document.getElementById('editSlotCode').textContent = slot.slotCode;
    document.getElementById('editSlotPosition').textContent = `${slot.zone} · ${slot.rowNumber}排${slot.columnNumber}列${slot.levelNumber}层`;
    document.getElementById('editInternalNumber').value = slot.internalNumber || 0;
    document.getElementById('editIsEnabled').checked = slot.isEnabled !== false;
    document.getElementById('slotEditModal').classList.remove('hidden');
}
function closeSlotEdit() {
    document.getElementById('slotEditModal').classList.add('hidden');
}
async function saveSlotEdit() {
    const slotCode = document.getElementById('editSlotCode').textContent;
    const internalNumber = parseInt(document.getElementById('editInternalNumber').value) || 0;
    const isEnabled = document.getElementById('editIsEnabled').checked;
    try {
        await api('/storageslots', {
            method: 'PUT',
            body: JSON.stringify({ slotCode, internalNumber, isEnabled })
        });
        toast('✅ 库位已更新', 'success');
        closeSlotEdit();
        await loadSlotPage();
    } catch(e) { toast('❌ 保存失败: ' + e.message, 'error'); }
}
async function abnormalReleaseSlot() {
    const slotCode = document.getElementById('editSlotCode').textContent;
    if (!confirm(`确定要异常释放库位 ${slotCode} 吗？\n这将强制清空该库位上的所有工件信息。`)) return;
    try {
        await api(`/storageslots/${encodeURIComponent(slotCode)}/abnormal-release`, { method: 'POST' });
        toast('✅ 异常释放成功', 'success');
        closeSlotEdit();
        await loadSlotPage();
    } catch(e) { toast('❌ 异常释放失败: ' + e.message, 'error'); }
}

async function loadSlotPage() {
    try {
        const state = await api('/appstate');
        editingSlotData = state.slots;
        renderSlotVisual('slotMgmtSlots', state.slots);
        document.getElementById('slotTableBody').innerHTML = state.slots.map(s => {
            const isDisabled = s.isEnabled === false;
            const statusText = isDisabled ? '禁用' : (s.isOccupied ? '占用' : '空闲');
            const statusKey = isDisabled ? 'disabled' : (s.isOccupied ? 'occupied' : 'free');
            return `<tr data-status="${statusKey}">
                <td>${String(s.internalNumber).padStart(3, '0')}</td>
                <td>${s.slotCode}</td>
                <td><span class="badge ${isDisabled ? 'badge-viewer' : (s.isOccupied ? 'badge-inactive' : 'badge-active')}">${statusText}</span></td>
                <td>${s.rowNumber}排/${s.columnNumber}列/${s.levelNumber}层</td>
                <td>${isDisabled ? '❌' : '✅'}</td>
                <td><button class="btn btn-sm btn-primary" onclick="openSlotEdit('${s.slotCode}')">✏️ 编辑</button></td>
            </tr>`;
        }).join('');
        document.getElementById('slotInventoryBody').innerHTML = state.inventory.slice(0,20).map(r =>
            `<tr><td>${r.palletNumber}</td><td>${r.modelType}</td><td>${r.slotCode}</td><td>${formatTime(r.inboundTime)}</td></tr>`
        ).join('') || '<tr><td colspan="4" class="empty-state">暂无数据</td></tr>';
    } catch(e) { toast('加载失败', 'error'); }
}

// ===== 台账 =====
async function queryLedger() {
    try {
        const payload = {
            type: document.getElementById('ledgerType').value ? parseInt(document.getElementById('ledgerType').value) : null,
            palletNumber: document.getElementById('ledgerPallet').value.trim() || null,
            workOrder: document.getElementById('ledgerWorkOrder').value.trim() || null,
            operatorName: document.getElementById('ledgerOperator').value.trim() || null,
            startTime: document.getElementById('ledgerStart').value ? new Date(document.getElementById('ledgerStart').value).toISOString() : null,
            endTime: document.getElementById('ledgerEnd').value ? new Date(document.getElementById('ledgerEnd').value).toISOString() : null
        };
        const entries = await api('/ledgerentries/query', { method: 'POST', body: JSON.stringify(payload) });
        document.getElementById('ledgerBody').innerHTML = entries.map(e =>
            `<tr><td>${formatTime(e.timestamp)}</td>
            <td><span class="badge ${e.type === 0 ? 'badge-admin' : 'badge-inactive'}">${e.type === 0 ? '入库' : '出库'}</span></td>
            <td>${e.palletNumber}</td><td>${e.workOrder}</td><td>${e.operatorName}</td><td>${e.slotCode}</td><td>${e.actionDescription}</td></tr>`
        ).join('') || '<tr><td colspan="7" class="empty-state">无匹配记录</td></tr>';
    } catch(e) { toast('查询失败', 'error'); }
}
function resetLedger() {
    document.getElementById('ledgerType').value = '';
    ['ledgerPallet','ledgerWorkOrder','ledgerOperator'].forEach(id => document.getElementById(id).value = '');
    const now = new Date();
    document.getElementById('ledgerStart').value = new Date(now.getTime() - 7*86400000).toISOString().slice(0,16);
    document.getElementById('ledgerEnd').value = now.toISOString().slice(0,16);
    queryLedger();
}

// ===== 报表 =====
async function previewReport() {
    try { const d = await api('/ledgerentries/report', { method:'POST', body:JSON.stringify({reportType:document.getElementById('reportType').value}) });
        document.getElementById('reportPreview').textContent = (d.csvLines||[]).join('\n'); }
    catch(e) { toast('生成失败', 'error'); }
}
async function exportCSV() {
    try { const d = await api('/ledgerentries/report', { method:'POST', body:JSON.stringify({reportType:document.getElementById('reportType').value}) });
        const blob = new Blob(['\uFEFF' + (d.csvLines||[]).join('\n')], {type:'text/csv;charset=utf-8;'});
        const a = document.createElement('a'); a.href = URL.createObjectURL(blob);
        a.download = `${document.getElementById('reportType').value}-${new Date().toISOString().slice(0,10)}.csv`;
        a.click(); toast('✅ 导出成功', 'success'); }
    catch(e) { toast('❌ 导出失败', 'error'); }
}
async function saveAlertSettings() {
    const min = parseInt(document.getElementById('alertMin').value)||2, max = parseInt(document.getElementById('alertMax').value)||18;
    if (min > max) { toast('下限不能大于上限', 'error'); return; }
    try { await api('/appstate/alerts', { method:'POST', body:JSON.stringify({minThreshold:min, maxThreshold:max}) }); toast('✅ 已保存', 'success'); }
    catch(e) { toast('保存失败', 'error'); }
    // 如果在预警页面则刷新状态
    if (document.getElementById('page-alert').classList.contains('active')) loadAlertPage();
}

// ===== 预警页面 =====
async function loadAlertPage() {
    try {
        const state = await api('/appstate');
        const alert = state.alertSettings || {};
        document.getElementById('alertMin').value = alert.minThreshold ?? 2;
        document.getElementById('alertMax').value = alert.maxThreshold ?? 18;
        const count = state.inventory?.length || 0;
        const minVal = alert.minThreshold ?? 2;
        const maxVal = alert.maxThreshold ?? 18;
        const status = count < minVal ? '⚠️ 低于下限' : count > maxVal ? '⚠️ 高于上限' : '✅ 正常';
        document.getElementById('alertStatus').innerHTML =
            `<span class="badge ${count < minVal || count > maxVal ? 'badge-inactive' : 'badge-active'}" style="font-size:16px;padding:4px 16px">${status}</span>`;
        document.getElementById('alertDetail').innerHTML =
            `当前库存：<strong>${count}</strong> 件 · 下限：<strong>${minVal}</strong> · 上限：<strong>${maxVal}</strong>`;
    } catch(e) { toast('加载预警数据失败', 'error'); }
}

// ===== 用户管理 =====
let editingUserId = null;

async function loadUserList() {
    try {
        const users = await api('/auth/users');
        const roleMap = { Admin: '管理员', Operator: '操作员', Viewer: '查看员' };
        document.getElementById('userTableBody').innerHTML = users.map(u => `
            <tr>
                <td><strong>${u.username}</strong></td>
                <td>${u.displayName}</td>
                <td><span class="badge badge-${u.role.toLowerCase()}">${roleMap[u.role] || u.role}</span></td>
                <td><span class="badge ${u.isActive ? 'badge-active' : 'badge-inactive'}">${u.isActive ? '启用' : '停用'}</span></td>
                <td>${formatTime(u.createdAt)}</td>
                <td>
                    ${u.id === BUILT_IN_USER_ID ? '<span style="color:#98a2b3;font-size:12px">🔒 内置账号</span>' :
                        `<button class="btn btn-sm btn-warning" onclick="editUser('${u.id}','${encodeURIComponent(u.username)}','${encodeURIComponent(u.displayName)}','${u.role}','${u.isActive}')">✏️ 编辑</button>
                    <button class="btn btn-sm btn-danger" onclick="deleteUser('${u.id}')">🗑️ 删除</button>`}
                </td>
            </tr>
        `).join('');
    } catch(e) {
        document.getElementById('userTableBody').innerHTML = '<tr><td colspan="6" class="empty-state">加载失败: ' + e.message + '</td></tr>';
        toast('用户加载失败: ' + e.message, 'error');
    }
}

function showCreateUserModal() {
    editingUserId = null;
    document.getElementById('modalTitle').textContent = '新建用户';
    document.getElementById('mUsername').value = ''; document.getElementById('mUsername').disabled = false;
    document.getElementById('mPassword').value = ''; document.getElementById('mPassword').required = true;
    document.getElementById('mDisplayName').value = ''; document.getElementById('mRole').value = 'Operator';
    document.getElementById('mIsActive').checked = true;
    document.getElementById('userModal').classList.remove('hidden');
}

function editUser(id, username, displayName, role, isActive) {
    editingUserId = id;
    username = decodeURIComponent(username);
    displayName = decodeURIComponent(displayName);
    document.getElementById('modalTitle').textContent = `编辑用户 - ${username}`;
    document.getElementById('mUsername').value = username; document.getElementById('mUsername').disabled = true;
    document.getElementById('mPassword').value = ''; document.getElementById('mPassword').required = false;
    document.getElementById('mDisplayName').value = displayName;
    document.getElementById('mRole').value = role;
    document.getElementById('mIsActive').checked = String(isActive).toLowerCase() === 'true';
    document.getElementById('userModal').classList.remove('hidden');
}

async function saveUser() {
    const username = document.getElementById('mUsername').value.trim();
    const password = document.getElementById('mPassword').value;
    const displayName = document.getElementById('mDisplayName').value.trim() || username;
    const role = document.getElementById('mRole').value;
    const isActive = document.getElementById('mIsActive').checked;

    if (!username) { toast('用户名不能为空', 'error'); return; }

    try {
        if (editingUserId) {
            // 更新
            await api(`/auth/users/${editingUserId}`, { method: 'PUT', body: JSON.stringify({ displayName, role, isActive }) });
            if (password) {
                await api(`/auth/users/${editingUserId}/password`, { method: 'PUT', body: JSON.stringify({ newPassword: password }) });
            }
            toast('✅ 用户已更新', 'success');
        } else {
            // 新建
            if (!password) { toast('密码不能为空', 'error'); return; }
            await api('/auth/create-user', { method: 'POST', body: JSON.stringify({ username, password, displayName, role }) });
            toast('✅ 用户已创建', 'success');
        }
        document.getElementById('userModal').classList.add('hidden');
        await loadUserList();
    } catch(e) { toast('❌ ' + e.message, 'error'); }
}

async function deleteUser(id) {
    if (!confirm('确定删除此用户？')) return;
    try {
        await api(`/auth/users/${id}`, { method: 'DELETE' });
        toast('✅ 用户已删除', 'success');
        await loadUserList();
    } catch(e) { toast('❌ ' + e.message, 'error'); }
}

function closeUserModal() { document.getElementById('userModal').classList.add('hidden'); }

// ===== 设备监控 =====
let devMonitorTimer = null;

async function loadDeviceMonitor() {
    try {
        const data = await api('/device/monitor');
        const st = data.status, pos = data.position, fl = data.flags, cmd = data.command;

        // 状态卡片
        const stateMap = { 1: '<span style="color:#12b76a">🟢 空闲中</span>', 2: '<span style="color:#f79009">🟡 运行中</span>', 3: '<span style="color:#d92d20">🔴 故障中</span>', 4: '<span style="color:#667085">⏸️ 暂停中</span>' };
        document.getElementById('devState').innerHTML = stateMap[st.state] || `未知(${st.state})`;
        document.getElementById('devMode').textContent = st.mode === 1 ? '🛠️ 手动' : st.mode === 2 ? '🤖 自动' : `-`;
        document.getElementById('devStep').textContent = st.step > 0 ? `步骤 ${st.step}` : '-';
        const errorEl = document.getElementById('devError');
        errorEl.textContent = st.errorCode || '0';
        errorEl.style.color = st.errorCode > 0 ? '#d92d20' : '#12b76a';

        // 轴位置
        document.getElementById('devPosition').innerHTML = `
            <div style="margin-bottom:4px">X轴: <strong>${pos.x}</strong> mm</div>
            <div style="margin-bottom:4px">Y轴: <strong>${pos.y}</strong> mm</div>
            <div style="margin-bottom:4px">Z深库: <strong>${pos.zDeep}</strong> mm</div>
            <div>Z浅库: <strong>${pos.zShallow}</strong> mm</div>
        `;

        // 信号状态
        document.getElementById('devFlags').innerHTML = `
            <div style="display:grid;grid-template-columns:1fr 1fr;gap:4px">
                <div>任务完成: <strong>${fl.taskDone ? '✅' : '❌'}</strong></div>
                <div>转移状态: <strong>${fl.transferState}</strong></div>
                <div>可移库: <strong>${data.canMove === 1 ? '✅' : '❌'}</strong></div>
                <div>左入可入库: <strong>${fl.leftIn === 1 ? '✅' : '❌'}</strong></div>
                <div>左出可出库: <strong>${fl.leftOut === 1 ? '✅' : '❌'}</strong></div>
                <div>右入可入库: <strong>${fl.rightIn === 1 ? '✅' : '❌'}</strong></div>
                <div>右出可出库: <strong>${fl.rightOut === 1 ? '✅' : '❌'}</strong></div>
                <div>A→B完成: <strong>${fl.actionDone ? '✅' : '❌'}</strong></div>
                <div>堆垛车位置: <strong>${fl.carrierPos || '-'}</strong></div>
            </div>
        `;

        document.getElementById('devLastUpdate').textContent = `🕐 更新于 ${new Date().toLocaleTimeString('zh-CN')}`;

        // 寄存器表
        const allRegs = [
            {a:4008,n:'设备序号',v:data.deviceNo ?? 0,d:''},
            {a:4009,n:'立库状态',v:st.state,d:'1空闲 2运行 3故障'},
            {a:4010,n:'故障代码',v:st.errorCode,d:''},{a:4011,n:'运行模式',v:st.mode,d:'1手动 2自动'},
            {a:4012,n:'动作步骤',v:st.step,d:''},{a:4013,n:'任务完成',v:fl.taskDone,d:'1完成 2执行'},
            {a:4014,n:'转移状态',v:fl.transferState,d:'1未取 2已转 3到位'},
            {a:4016,n:'X轴位置',v:pos.x,d:'mm'},{a:4017,n:'Y轴位置',v:pos.y,d:'mm'},
            {a:4018,n:'Z深库',v:pos.zDeep,d:'mm'},{a:4019,n:'Z浅库',v:pos.zShallow,d:'mm'},
            {a:4020,n:'可移库',v:data.canMove ?? 0,d:'1可 2不可'},{a:4021,n:'左入可入库',v:fl.leftIn,d:'1可 2不可'},
            {a:4022,n:'左出可出库',v:fl.leftOut,d:'1可 2不可'},{a:4023,n:'右入可入库',v:fl.rightIn,d:'1可 2不可'},
            {a:4024,n:'右出可出库',v:fl.rightOut,d:'1可 2不可'},{a:4025,n:'A→B完成',v:fl.actionDone,d:'0/1'},
            {a:4026,n:'堆垛车位置',v:fl.carrierPos,d:'车位号'},{a:2022,n:'设备序号',v:data.deviceNo ?? 0,d:'写入区'},
            {a:2023,n:'动作序列/标志',v:cmd.seqNo,d:'写入区'},{a:2024,n:'排',v:cmd.row ?? 0,d:'写入区'},
            {a:2025,n:'列',v:cmd.col ?? 0,d:'写入区'},{a:2026,n:'层',v:cmd.level ?? 0,d:'写入区'}
        ];
        document.getElementById('devRegisterTable').innerHTML = allRegs.map(r =>
            `<tr><td>D${r.a}</td><td>${r.n}</td><td style="font-weight:bold;font-size:14px">${r.v}</td><td style="color:var(--text-secondary);font-size:12px">${r.d}</td></tr>`
        ).join('');

        // 自动刷新 - 仅连接成功时才快速刷新，否则慢速
        const isActive = document.getElementById('page-devmonitor').classList.contains('active');
        clearTimeout(devMonitorTimer);
        if (isActive) {
            const interval = data.connected ? 2000 : 10000;
            devMonitorTimer = setTimeout(loadDeviceMonitor, interval);
        }
    } catch(e) {
        if (document.getElementById('page-devmonitor').classList.contains('active'))
            devMonitorTimer = setTimeout(loadDeviceMonitor, 10000);
    }
}

// ===== 设备调用 =====
async function loadDeviceRegisters() {
    try {
        const defs = await api('/device/write-defs');
        const regs = defs.registers || [];
        const current = await api('/device/monitor');
        const cmd = current.command || {};

        document.getElementById('writeRegisters').innerHTML = regs.map(r => {
            const val = cmd[getCmdKey(r.address)] ?? 0;
            return `<div style="padding:8px;background:#f9fafb;border-radius:6px;border:1px solid var(--border)">
                <div style="font-size:11px;color:var(--text-secondary)">D${r.address} ${r.name}</div>
                <input type="number" id="reg_${r.address}" value="${val}" min="0" max="65535"
                    style="width:100%;padding:4px 8px;margin-top:4px;border:1px solid var(--border);border-radius:4px;font-size:13px">
                <div style="font-size:10px;color:var(--text-secondary);margin-top:2px">${r.description}</div>
            </div>`;
        }).join('');
    } catch(e) { toast('加载寄存器定义失败', 'error'); }
}

function getCmdKey(addr) {
    const map = {2022:'deviceNo',2023:'actionFlag',2024:'row',2025:'col',2026:'level'};
    return map[addr] || '';
}

async function sendDeviceCommand() {
    const cmd = {
        deviceNo: parseInt(document.getElementById('cmdDeviceNo').value) || 1,
        actionFlag: parseInt(document.getElementById('cmdActionFlag').value) || 1,
        fromRow: parseInt(document.getElementById('cmdFromRow').value) || 1,
        fromCol: parseInt(document.getElementById('cmdFromCol').value) || 1,
        fromLevel: parseInt(document.getElementById('cmdFromLevel').value) || 1
    };

    try {
        const result = await api('/device/command', { method: 'POST', body: JSON.stringify(cmd) });
        const el = document.getElementById('cmdResult');
        el.style.display = 'block';
        el.innerHTML = `<div style="color:var(--success);font-weight:bold">✅ ${result.message}</div>`;
        // 自动刷新监控
        if (document.getElementById('page-devcontrol').classList.contains('active')) {
            setTimeout(() => { loadDeviceMonitor(); loadDeviceRegisters(); }, 500);
        }
    } catch(e) {
        const el = document.getElementById('cmdResult');
        el.style.display = 'block';
        el.innerHTML = `<div style="color:var(--danger)">❌ 指令发送失败: ${e.message}</div>`;
    }
}

async function writeSingleRegister() {
    const addr = parseInt(document.getElementById('singleAddr').value);
    const val = parseInt(document.getElementById('singleValue').value);
    if (isNaN(addr) || isNaN(val)) { toast('请输入地址和值', 'error'); return; }
    try {
        await api('/device/write', { method: 'POST', body: JSON.stringify({ address: addr, value: val }) });
        toast(`✅ D${addr} = ${val} 已写入`, 'success');
        loadDeviceRegisters(); loadDeviceMonitor();
    } catch(e) { toast('❌ ' + e.message, 'error'); }
}

// ===== 角色权限管理 =====
let rolePermissionCache = {};

async function loadRolePage() {
    try {
        const roles = await api('/roles');
        rolePermissionCache = {};
        const container = document.getElementById('rolePermissionsContainer');
        container.innerHTML = roles.map(r => {
            rolePermissionCache[r.role] = r.allowedPages || [];
            const pages = r.allPages || [];
            return `<div class="card" style="margin-bottom:12px">
                <div class="card-header" style="font-size:14px">${r.roleDisplayName} <span style="font-weight:normal;color:var(--text-secondary)">(${r.role})</span></div>
                <div class="card-body">
                    <div style="display:flex;flex-wrap:wrap;gap:10px" id="rolePages_${r.role}">
                        ${pages.map(p => `
                            <label style="display:flex;align-items:center;gap:6px;padding:6px 12px;border:1px solid var(--border);border-radius:6px;cursor:pointer;background:${p.allowed ? '#e8f0fe' : '#f9fafb'}"
                                onclick="toggleRolePage('${r.role}','${p.id}',this)">
                                <input type="checkbox" ${p.allowed ? 'checked' : ''} style="width:16px;height:16px">
                                <span style="font-size:13px">${p.name}</span>
                            </label>
                        `).join('')}
                    </div>
                    <div class="btn-group">
                        <button class="btn btn-primary btn-sm" onclick="saveRolePermissions('${r.role}')">💾 保存 ${r.roleDisplayName}</button>
                        <button class="btn btn-sm" onclick="resetRolePermissions('${r.role}')">🔄 恢复默认</button>
                    </div>
                </div>
            </div>`;
        }).join('');
    } catch(e) {
        document.getElementById('rolePermissionsContainer').innerHTML = '<div class="empty-state">加载失败: ' + e.message + '</div>';
        toast('角色加载失败: ' + e.message, 'error');
    }
}

function toggleRolePage(role, pageId, labelEl) {
    if (!rolePermissionCache[role]) rolePermissionCache[role] = [];
    const checkbox = labelEl.querySelector('input');
    if (checkbox.checked) {
        if (!rolePermissionCache[role].includes(pageId)) rolePermissionCache[role].push(pageId);
    } else {
        rolePermissionCache[role] = rolePermissionCache[role].filter(p => p !== pageId);
    }
    labelEl.style.background = checkbox.checked ? '#e8f0fe' : '#f9fafb';
}

async function saveRolePermissions(role) {
    const pages = rolePermissionCache[role] || [];
    try {
        await api(`/roles/${role}`, { method: 'POST', body: JSON.stringify({ role, pages }) });
        toast(`✅ ${role} 权限已保存`, 'success');
    } catch(e) { toast('❌ 保存失败: ' + e.message, 'error'); }
}

async function resetRolePermissions(role) {
    const defaults = { Admin: ['dashboard','inbound','outbound','slots','ledger','report','alert','users','devmonitor','devcontrol','roles'],
        Operator: ['dashboard','inbound','outbound','slots','ledger','report','alert','devmonitor','devcontrol'],
        Viewer: ['dashboard','slots','ledger','report'] };
    rolePermissionCache[role] = defaults[role] || [];
    await loadRolePage();
    toast(`🔄 ${role} 已恢复默认权限`, 'success');
}
