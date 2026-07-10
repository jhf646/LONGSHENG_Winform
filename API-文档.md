# 上海隆深库存管理系统 - 前后端接口文档

## 概述

系统采用前后端分离架构：
- **后端 API**：`LongShenStorageApi`（ASP.NET Core Web API，运行于 `http://localhost:5000`）
- **WinForms 客户端**：`LongShenStorageSystem`（原生 .NET WinForms 桌面应用）
- **Web 前端**：`LongShenStorageWeb`（HTML/CSS/JS 单页应用）

---

## 目录

1. [系统状态与仪表盘](#1-系统状态与仪表盘)
2. [工件记录（入库/出库）](#2-工件记录入库出库)
3. [货位管理](#3-货位管理)
4. [台账查询与报表](#4-台账查询与报表)

---

## 1. 系统状态与仪表盘

### `GET /api/appstate`
获取完整系统状态

**响应示例：**
```json
{
  "inventory": [ ... ],
  "slots": [ ... ],
  "ledger": [ ... ],
  "alertSettings": { "minThreshold": 2, "maxThreshold": 18 },
  "palletNumbers": ["T1","T2",...],
  "toolingNumbers": [],
  "projectNumbers": [],
  "modelTypes": [],
  "customerNames": []
}
```

### `GET /api/appstate/dashboard`
获取仪表盘汇总数据

**响应：**
```json
{
  "inventoryCount": 5,
  "occupiedSlots": 5,
  "freeSlots": 59,
  "alertStatus": "正常",
  "isAlert": false,
  "slots": [ ... ],
  "recentInventory": [ ... ]
}
```

### `GET /api/appstate/dropdowns`
获取下拉选项列表

**响应：**
```json
{
  "palletNumbers": ["T1","T2",...,"T66"],
  "toolingNumbers": [],
  "projectNumbers": [],
  "modelTypes": [],
  "customerNames": []
}
```

### `POST /api/appstate/alerts`
保存预警阈值

**请求体：**
```json
{ "minThreshold": 2, "maxThreshold": 18 }
```

### `POST /api/appstate/dropdowns`
保存用户新增的下拉选项

**请求体：**
```json
{
  "palletNumbers": ["T67"],
  "toolingNumbers": ["工装A"],
  "projectNumbers": [],
  "modelTypes": [],
  "customerNames": []
}
```

---

## 2. 工件记录（入库/出库）

### `GET /api/workpiecerecords`
获取所有在库工件列表

**响应：** `WorkpieceRecord[]`

### `GET /api/workpiecerecords/{id}`
根据 ID 获取单个工件

### `POST /api/workpiecerecords/inbound`
执行入库操作

**请求体：**
```json
{
  "palletNumber": "T1",
  "toolingNumber": "工装A",
  "projectNumber": "项目X",
  "modelType": "ABC-123",
  "workOrder": "WO-2026-001",
  "cellNumber": "CELL-01",
  "componentSections": 5,
  "customerName": "客户A",
  "operatorName": "张三",
  "specifiedSlot": null,
  "notes": "备注信息"
}
```

**响应：** 创建的 `WorkpieceRecord` 对象

**错误：** 400 - `{"error": "当前无空闲货位可分配或指定货位不可用"}`

### `POST /api/workpiecerecords/outbound`
执行出库操作

**请求体：**
```json
{
  "recordId": "guid-xxxx-xxxx",
  "operatorName": "张三",
  "specifiedSlot": null
}
```

**响应：** 200 - `{"message": "出库成功"}`

**错误：** 400 - `{"error": "出库失败，请检查工件编号或货位"}`

---

## 3. 货位管理

### `GET /api/storageslots`
获取所有货位列表

### `GET /api/storageslots/next-available`
获取下一个空闲货位

**响应：**
```json
{ "slotCode": "1排-1列-1层", "message": "推荐空闲货位：1排-1列-1层" }
```

### `POST /api/storageslots/{slotCode}/release`
释放指定货位（仅空闲状态允许）

**响应：** `{"message": "货位 1排-1列-1层 已释放"}`

**错误：** 400 - `{"error": "货位当前有工件占用，不能释放"}`

---

## 4. 台账查询与报表

### `GET /api/ledgerentries`
获取最近 100 条台账记录

**响应：** `LedgerEntry[]`

### `POST /api/ledgerentries/query`
多条件查询台账

**请求体：**
```json
{
  "type": 0,
  "palletNumber": "T1",
  "workOrder": "WO-001",
  "operatorName": "张三",
  "startTime": "2026-01-01T00:00:00Z",
  "endTime": "2026-12-31T23:59:59Z"
}
```
> `type`: `0`=入库, `1`=出库, 不传=全部

### `POST /api/ledgerentries/report`
获取报表 CSV 文本内容

**请求体：**
```json
{
  "reportType": "进出台账",
  "minThreshold": 2,
  "maxThreshold": 18
}
```
> `reportType`: `"进出台账"` / `"库存清单"` / `"操作记录"`

**响应：**
```json
{
  "csvLines": ["时间,类型,操作人员,托盘号,...", "..."]
}
```

---

## 数据模型

### WorkpieceRecord（工件记录）
| 字段 | 类型 | 说明 |
|------|------|------|
| id | GUID | 唯一编号 |
| palletNumber | string | 托盘号 |
| toolingNumber | string | 工装号 |
| projectNumber | string | 项目号 |
| modelType | string | 型号 |
| workOrder | string | 工单号 |
| cellNumber | string | 电解槽编号 |
| componentSections | int | 组件节数(1-20) |
| customerName | string | 客户名称 |
| inboundTime | datetime | 入库时间 |
| slotCode | string | 货位号 |
| lastOperator | string | 操作人员 |
| lastUpdated | datetime | 最后更新 |
| notes | string | 备注 |

### StorageSlot（货位）
| 字段 | 类型 | 说明 |
|------|------|------|
| slotCode | string | 货位号(如"1排-1列-1层") |
| isOccupied | bool | 是否占用 |
| workpieceId | GUID? | 关联工件编号 |
| zone | string | 库区 |
| rowNumber | int | 排(1-2) |
| columnNumber | int | 列(1-4) |
| levelNumber | int | 层(1-8) |

### LedgerEntry（台账）
| 字段 | 类型 | 说明 |
|------|------|------|
| id | GUID | 编号 |
| type | int | 0=入库, 1=出库 |
| timestamp | datetime | 操作时间 |
| operatorName | string | 操作人员 |
| palletNumber | string | 托盘号 |
| toolingNumber | string | 工装号 |
| projectNumber | string | 项目号 |
| modelType | string | 型号 |
| workOrder | string | 工单号 |
| cellNumber | string | 电解槽编号 |
| componentSections | int | 组件节数 |
| customerName | string | 客户名称 |
| slotCode | string | 货位号 |
| actionDescription | string | 操作说明 |

---

## 部署与运行

### 后端 API
```bash
cd LongShenStorageApi
dotnet run
# 启动后访问 http://localhost:5000 自动跳转 Swagger 文档
```

### Web 前端
直接浏览器打开 `LongShenStorageWeb/index.html`，确保 API 已启动。

### WinForms 客户端
```bash
cd LongShenStorageSystem
dotnet run
```
（当前 WinForms 客户端仍为直连数据库模式，可后续改造为 API 调用模式）
