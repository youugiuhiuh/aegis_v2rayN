# Todo List: v2rayN XDRIVE Transport Support

- [x] Task 1: Enums & Core Models
  - [x] Add `xdrive` to `ETransport.cs` and `Global.Networks` in `Global.cs`
  - [x] Add xdrive properties to `TransportExtraItem.cs`
  - [x] Add `XdriveSettings4Ray` and `S3Settings4Ray` to `V2rayConfig.cs`
  - [x] Verify build passes
- [x] Task 2: Share URI Import/Export & Unit Tests (TDD)
  - [x] Write failing test in `ServiceLib.Tests` for `vless://` with `type=xdrive` (Google Drive & S3)
  - [x] Implement `ToUriQuery` and `ResolveUriQuery` in `BaseFmt.cs` for `xdrive`
  - [x] Verify tests pass
- [x] Task 3: Xray Outbound Generation & Tests (TDD)
  - [x] Write failing test in `CoreConfigV2rayOutboundTests` for XDRIVE streamSettings
  - [x] Implement `FillBoundStreamSettings` handling for `xdrive` with Mux in `V2rayOutboundService.cs`
  - [x] Verify tests pass
- [x] Task 4: UI & Localization
  - [x] Add UI strings in `ResUI.resx` and `ResUI.zh-Hans.resx`
  - [x] Bind XDRIVE properties in `AddServerViewModel.cs`
  - [x] Run full test suite (`mise x -- dotnet test` 660 passed)
