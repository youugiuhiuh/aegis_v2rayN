# Todo List: v2rayN XDRIVE Transport Support

- [ ] Task 1: Enums & Core Models
  - [ ] Add `xdrive` to `ETransport.cs` and `Global.Networks` in `Global.cs`
  - [ ] Add xdrive properties to `TransportExtraItem.cs`
  - [ ] Add `XdriveSettings4Ray` and `S3Settings4Ray` to `V2rayConfig.cs`
  - [ ] Verify build passes
- [ ] Task 2: Share URI Import/Export & Unit Tests (TDD)
  - [ ] Write failing test in `ServiceLib.Tests` for `vless://` with `type=xdrive` (Google Drive & S3)
  - [ ] Implement `ToUriQuery` and `ResolveUriQuery` in `BaseFmt.cs` for `xdrive`
  - [ ] Verify tests pass
- [ ] Task 3: Xray Outbound Generation & Tests (TDD)
  - [ ] Write failing test in `CoreConfigV2rayOutboundTests` for XDRIVE streamSettings
  - [ ] Implement `FillBoundStreamSettings` handling for `xdrive` with Mux in `V2rayOutboundService.cs`
  - [ ] Verify tests pass
- [ ] Task 4: UI & Localization
  - [ ] Add UI strings in `ResUI.resx` and `ResUI.zh-Hans.resx`
  - [ ] Bind XDRIVE properties in `AddServerViewModel.cs`
  - [ ] Run full test suite (`mise x -- dotnet test`)
