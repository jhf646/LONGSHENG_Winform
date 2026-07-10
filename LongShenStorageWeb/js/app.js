// ===== 隆深库存管理系统 Web 前端 =====
const API_BASE = 'http://localhost:5000/api';

// 工具函数
async function api(path, options = {}) {
    const resp = await fetch(`${API_BASE}${path}`, {
        headers: { 'Content-Type': 'application/json', ...options.headers },
        ...options
    });
    if (!resp.ok) {
        const err = await resp.json().catch(() => ({ error: resp.statusText }));
        throw new Error(err.error || `HTTP ${resp.status}`);
    }
    return resp.json();
}

function toast(msg, type = '') {
    const t = document.createElement('div');
    t.className = `toast ${type}`;
    t.textContent = msg;
    document.body.appendChild(t);
    setTimeout(() => t.remove(), 3000);
}

function formatTime(d) {
    const dt = new Date(d);
    return dt.toLocaleString('zh-CN', { hour12: false });
}

// ===== Tab 切换 =====
document.getElementById('tabNav').addEventListener('click', e => {
    const btn = e.target.closest('.tab-btn');
    if (!btn) return;
    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
    btn.classList.add('active');
    document.getElementById(`tab-${btn.dataset.tab}`).classList.add('active');
    if (btn.dataset.tab === 'dashboard') loadDashboard();
    if (btn.dataset.tab === 'slots') loadSlotPage();
});

// ===== 初始化 =====
document.addEventListener('DOMContentLoaded', async () => {
    await Promise.all([loadDashboard(), loadDropdowns()]);
    loadOutboundOptions();
    // 默认设置台账时间
    const now = new Date();
    const weekAgo = new Date(now.getTime() - 7 * 86400000);
    document.getElementById('ledgerStart').value = weekAgo.toISOString().slice(0, 16);
    document.getElementById('ledgerEnd').value = now.toISOString().slice(0, 16);
});

// ===== 下拉选项 =====
async function loadDropdowns() {
    try {
        const data = await api('/appstate/dropdowns');
        setDatalist('dlPallet', data.palletNumbers || []);
        setDatalist('dlTooling', data.toolingNumbers || []);
        setDatalist('dlProject', data.projectNumbers || []);
        setDatalist('dlModel', data.modelTypes || []);
        setDatalist('dlCustomer', data.customerNames || []);
    } catch (e) { console.warn('加载下拉选项失败:', e); }
}

function setDatalist(id, items) {
    const dl = document.getElementById(id);
    dl.innerHTML = items.map(v => `<option value="${v}">`).join('');
}

async function saveNewComboItem() {
    const pallet = document.getElementById('inPallet').value.trim();
    const tooling = document.getElementById('inTooling').value.trim();
    const project = document.getElementById('inProject').value.trim();
    const model = document.getElementById('inModel').value.trim();
    const customer = document.getElementById('inCustomer').value.trim();
    try {
        await api('/appstate/dropdowns', {
            method: 'POST',
            body: JSON.stringify({
                palletNumbers: pallet ? [pallet] : [],
                toolingNumbers: tooling ? [tooling] : [],
                projectNumbers: project ? [project] : [],
                modelTypes: model ? [model] : [],
                customerNames: customer ? [customer] : []
            })
        });
    } catch (e) { console.warn('保存下拉选项失败:', e); }
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
        alertEl.style.color = data.isAlert ? '#bb4a39' : '#222e3a';
        document.getElementById('headerStatus').textContent = `库存状态：${data.inventoryCount}件 | ${data.occupiedSlots}占用 | ${data.freeSlots}空闲 | ${data.alertStatus}`;

        // 货位可视化
        renderSlotVisual('dashboardSlots', data.slots);

        // 最近入库
        const tbody = document.getElementById('recentBody');
        tbody.innerHTML = (data.recentInventory || []).slice(0, 10).map(r =>
            `<tr><td>${r.palletNumber}</td><td>${r.toolingNumber}</td><td>${r.slotCode}</td><td>${r.lastOperator}</td><td>${formatTime(r.inboundTime)}</td></tr>`
        ).join('') || '<tr><td colspan="5" style="text-align:center">暂无数据</td></tr>';
    } catch (e) {
        toast('加载仪表盘失败: ' + e.message, 'error');
    }
}

// ===== 货位可视化渲染 =====
function renderSlotVisual(containerId, slots) {
    const container = document.getElementById(containerId);
    if (!container || !slots) return;
    container.innerHTML = '';

    for (let row = 1; row <= 2; row++) {
        const rack = document.createElement('div');
        rack.className = 'slot-rack';

        const header = document.createElement('h4');
        header.textContent = `第 ${row} 排`;
        rack.appendChild(header);

        const grid = document.createElement('div');
        grid.className = 'rack-grid';

        // 空角
        const corner = document.createElement('div');
        corner.className = 'header-cell';
        corner.textContent = '层/列';
        grid.appendChild(corner);

        for (let c = 1; c <= 4; c++) {
            const hc = document.createElement('div');
            hc.className = 'header-cell';
            hc.textContent = `${c} 列`;
            grid.appendChild(hc);
        }

        for (let level = 8; level >= 1; level--) {
            const lh = document.createElement('div');
            lh.className = 'header-cell';
            lh.textContent = `${level}层`;
            grid.appendChild(lh);

            for (let col = 1; col <= 4; col++) {
                const slot = slots.find(s => s.rowNumber === row && s.columnNumber === col && s.levelNumber === level);
                const cell = document.createElement('div');
                cell.className = `cell ${slot?.isOccupied ? 'occupied' : 'free'}`;
                if (slot) {
                    cell.innerHTML = `<div class="slot-status">${slot.isOccupied ? '占用' : '空闲'}</div><div class="slot-code">${slot.slotCode}</div>`;
                    cell.title = slot.slotCode + (slot.isOccupied ? ' (占用)' : ' (空闲)');
                    cell.onclick = () => handleSlotClick(slot);
                }
                grid.appendChild(cell);
            }
        }

        rack.appendChild(grid);
        container.appendChild(rack);
    }
}

// ===== 入库 =====
let lastWorkOrder = '';
let lastCellNumber = '';

function clearInbound() {
    document.getElementById('inPallet').value = '';
    document.getElementById('inTooling').value = '';
    document.getElementById('inProject').value = '';
    document.getElementById('inModel').value = '';
    // 批次入库保留工单号和电解槽编号
    // document.getElementById('inWorkOrder').value = '';
    // document.getElementById('inCellNumber').value = '';
    document.getElementById('inComponentSections').value = '1';
    document.getElementById('inCustomer').value = '';
    document.getElementById('inOperator').value = '';
    document.getElementById('inSlot').value = '';
    document.getElementById('inNotes').value = '';
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
        toast('入库成功', 'success');
        // 保留批次字段
        lastWorkOrder = payload.workOrder;
        lastCellNumber = payload.cellNumber;
        clearInbound();
        document.getElementById('inWorkOrder').value = lastWorkOrder;
        document.getElementById('inCellNumber').value = lastCellNumber;
        await saveNewComboItem();
        await Promise.all([loadDashboard(), loadDropdowns()]);
        loadInboundRecent();
    } catch (e) {
        toast('入库失败: ' + e.message, 'error');
    }
}

async function loadInboundRecent() {
    try {
        const entries = await api('/ledgerentries');
        const tbody = document.getElementById('inboundRecentBody');
        tbody.innerHTML = entries.slice(0, 15).map(e =>
            `<tr><td>${formatTime(e.timestamp)}</td><td>${e.palletNumber}</td><td>${e.operatorName}</td><td>${e.slotCode}</td><td>${e.actionDescription}</td></tr>`
        ).join('') || '<tr><td colspan="5" style="text-align:center">暂无数据</td></tr>';
    } catch (e) { /* ignore */ }
}

// ===== 出库 =====
async function loadOutboundOptions() {
    try {
        const items = await api('/workpiecerecords');
        const sel = document.getElementById('outRecord');
        sel.innerHTML = items.map(r => `<option value="${r.id}">${r.palletNumber} - ${r.modelType} [${r.slotCode}]</option>`).join('') || '<option value="">暂无在库工件</option>';
    } catch (e) { toast('加载工件列表失败', 'error'); }
}

async function handleOutbound() {
    const recordId = document.getElementById('outRecord').value;
    const operator = document.getElementById('outOperator').value.trim();
    if (!recordId) { toast('请选择出库工件', 'error'); return; }
    if (!operator) { toast('请输入操作人员', 'error'); return; }

    try {
        await api('/workpiecerecords/outbound', {
            method: 'POST',
            body: JSON.stringify({
                recordId: recordId,
                operatorName: operator,
                specifiedSlot: document.getElementById('outSlot').value.trim() || null
            })
        });
        toast('出库成功', 'success');
        document.getElementById('outOperator').value = '';
        document.getElementById('outSlot').value = '';
        await Promise.all([loadDashboard(), loadOutboundOptions()]);
        loadOutboundRecent();
    } catch (e) {
        toast('出库失败: ' + e.message, 'error');
    }
}

async function loadOutboundRecent() {
    try {
        const entries = await api('/ledgerentries');
        const tbody = document.getElementById('outboundBody');
        tbody.innerHTML = entries.filter(e => e.type === 1).slice(0, 15).map(e =>
            `<tr><td>${formatTime(e.timestamp)}</td><td>${e.palletNumber}</td><td>${e.operatorName}</td><td>${e.actionDescription}</td></tr>`
        ).join('') || '<tr><td colspan="4" style="text-align:center">暂无数据</td></tr>';
    } catch (e) { /* ignore */ }
}

// ===== 货位管理 =====
let selectedSlotCode = null;

function handleSlotClick(slot) {
    selectedSlotCode = slot.slotCode;
    if (slot.isOccupied) {
        // 显示详情
        showSlotDetail(slot);
    } else {
        // 跳转到入库
        document.getElementById('inSlot').value = slot.slotCode;
        document.querySelector('[data-tab="inbound"]').click();
        document.getElementById('inPallet').focus();
    }
}

async function showSlotDetail(slot) {
    try {
        const items = await api('/workpiecerecords');
        const wp = items.find(i => i.id === slot.workpieceId);
        const msg = [
            `库位号：${slot.slotCode}`,
            `货架位置：${slot.rowNumber}排 ${slot.columnNumber}列 ${slot.levelNumber}层`,
            `状态：${slot.isOccupied ? '占用' : '空闲'}`,
            `托盘号：${wp?.palletNumber || '无'}`,
            `工装号：${wp?.toolingNumber || '无'}`,
            `项目号：${wp?.projectNumber || '无'}`,
            `型号：${wp?.modelType || '无'}`,
            `工单号：${wp?.workOrder || '无'}`,
            `电解槽编号：${wp?.cellNumber || '无'}`,
            `组件节数：${wp?.componentSections || 0}`,
            `客户名称：${wp?.customerName || '无'}`,
            `入库时间：${wp ? formatTime(wp.inboundTime) : '无'}`,
            `操作人员：${wp?.lastOperator || '无'}`,
            `备注：${wp?.notes || '无'}`
        ].join('\n');
        alert(msg);
    } catch (e) { toast('加载详情失败', 'error'); }
}

async function loadSlotPage() {
    try {
        const state = await api('/appstate');
        renderSlotVisual('slotMgmtSlots', state.slots);
        // 货位表格
        const tbody = document.getElementById('slotTableBody');
        tbody.innerHTML = state.slots.map(s =>
            `<tr style="cursor:pointer" onclick="selectedSlotCode='${s.slotCode}';loadSlotPage()">
                <td>${s.slotCode}</td>
                <td style="color:${s.isOccupied ? '#bb4a39' : '#388058'};font-weight:bold">${s.isOccupied ? '占用' : '空闲'}</td>
                <td>${s.rowNumber}排/${s.columnNumber}列/${s.levelNumber}层</td>
                <td>${s.workpieceId || '-'}</td>
            </tr>`
        ).join('');
        // 在库清单
        const invBody = document.getElementById('slotInventoryBody');
        invBody.innerHTML = state.inventory.slice(0, 20).map(r =>
            `<tr><td>${r.palletNumber}</td><td>${r.modelType}</td><td>${r.slotCode}</td><td>${formatTime(r.inboundTime)}</td></tr>`
        ).join('') || '<tr><td colspan="4" style="text-align:center">暂无数据</td></tr>';
    } catch (e) { toast('加载货位数据失败', 'error'); }
}

async function showNextAvailable() {
    try {
        const data = await api('/storageslots/next-available');
        toast(data.message, data.slotCode ? 'success' : 'error');
    } catch (e) { toast('查询失败', 'error'); }
}

async function releaseSelectedSlot() {
    if (!selectedSlotCode) { toast('请先在货位表格中点击一个空闲货位', 'error'); return; }
    try {
        await api(`/storageslots/${encodeURIComponent(selectedSlotCode)}/release`, { method: 'POST' });
        toast('货位已释放', 'success');
        selectedSlotCode = null;
        await loadSlotPage();
    } catch (e) { toast('释放失败: ' + e.message, 'error'); }
}

// ===== 台账查询 =====
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
        const tbody = document.getElementById('ledgerBody');
        tbody.innerHTML = entries.map(e =>
            `<tr>
                <td>${formatTime(e.timestamp)}</td>
                <td style="color:${e.type === 0 ? '#226894' : '#bb4a39'};font-weight:bold">${e.type === 0 ? '入库' : '出库'}</td>
                <td>${e.palletNumber}</td>
                <td>${e.workOrder}</td>
                <td>${e.operatorName}</td>
                <td>${e.slotCode}</td>
                <td>${e.actionDescription}</td>
            </tr>`
        ).join('') || '<tr><td colspan="7" style="text-align:center">暂无匹配记录</td></tr>';
    } catch (e) { toast('查询失败: ' + e.message, 'error'); }
}

function resetLedger() {
    document.getElementById('ledgerType').value = '';
    document.getElementById('ledgerPallet').value = '';
    document.getElementById('ledgerWorkOrder').value = '';
    document.getElementById('ledgerOperator').value = '';
    const now = new Date();
    const weekAgo = new Date(now.getTime() - 7 * 86400000);
    document.getElementById('ledgerStart').value = weekAgo.toISOString().slice(0, 16);
    document.getElementById('ledgerEnd').value = now.toISOString().slice(0, 16);
    queryLedger();
}

// ===== 报表 =====
async function previewReport() {
    try {
        const data = await api('/ledgerentries/report', {
            method: 'POST',
            body: JSON.stringify({
                reportType: document.getElementById('reportType').value
            })
        });
        document.getElementById('reportPreview').textContent = (data.csvLines || []).join('\n');
    } catch (e) { toast('生成报表失败', 'error'); }
}

async function exportCSV() {
    try {
        const data = await api('/ledgerentries/report', {
            method: 'POST',
            body: JSON.stringify({
                reportType: document.getElementById('reportType').value
            })
        });
        const csv = (data.csvLines || []).join('\n');
        const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${document.getElementById('reportType').value}-${new Date().toISOString().slice(0,8)}.csv`;
        a.click();
        URL.revokeObjectURL(url);
        toast('CSV 导出成功', 'success');
    } catch (e) { toast('导出失败: ' + e.message, 'error'); }
}

async function saveAlertSettings() {
    const min = parseInt(document.getElementById('alertMin').value) || 2;
    const max = parseInt(document.getElementById('alertMax').value) || 18;
    if (min > max) { toast('下限不能大于上限', 'error'); return; }
    try {
        await api('/appstate/alerts', {
            method: 'POST',
            body: JSON.stringify({ minThreshold: min, maxThreshold: max })
        });
        toast('预警阈值已保存', 'success');
    } catch (e) { toast('保存失败', 'error'); }
}
