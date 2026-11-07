# Implementation Plan: v2rayN XDRIVE Transport Support

## Background & Objectives
Support Xray-core's new `xdrive` object storage relay transport in v2rayN.
Covering:
- `ETransport.xdrive` and `Global.Networks`
- `XdriveSettings4Ray` and `S3Settings4Ray` in `V2rayConfig.cs`
- `TransportExtraItem` xdrive fields
- `BaseFmt.cs` URI import and export for `type=xdrive`
- `V2rayOutboundService.cs` outbound streamSettings and Mux generation
- `AddServerViewModel.cs` & `ResUI` UI support
- Full unit tests for round-trip share URI and Xray configuration generation

## Tasks Breakdown

### Group A: Core Models & URI Format (Parallelizable)
- **Task 1: Enums & Core Models**
  - Files:
    - `v2rayN/ServiceLib/Enums/ETransport.cs`
    - `v2rayN/ServiceLib/Global.cs`
    - `v2rayN/ServiceLib/Models/Entities/TransportExtraItem.cs`
    - `v2rayN/ServiceLib/Models/CoreConfigs/V2rayConfig.cs`
- **Task 2: Share URI Import/Export & Unit Tests (TDD)**
  - Files:
    - `v2rayN/ServiceLib/Handler/Fmt/BaseFmt.cs`
    - `v2rayN/ServiceLib.Tests/Handler/Fmt/VlessFmtTests.cs` (or `BaseFmtTests.cs`)

### Group B: CoreConfig Outbound Service & Generation Tests
- **Task 3: Xray Outbound Generation & Tests (TDD)**
  - Files:
    - `v2rayN/ServiceLib/Services/CoreConfig/V2ray/V2rayOutboundService.cs`
    - `v2rayN/ServiceLib.Tests/CoreConfig/V2ray/CoreConfigV2rayOutboundTests.cs`

### Group C: UI & Localization
- **Task 4: AddServerViewModel & ResUI**
  - Files:
    - `v2rayN/ServiceLib/ViewModels/AddServerViewModel.cs`
    - `v2rayN/ServiceLib/Resx/ResUI.resx`
    - `v2rayN/ServiceLib/Resx/ResUI.zh-Hans.resx`
