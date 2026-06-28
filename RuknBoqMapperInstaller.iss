; ==========================================================
; RUKN 5D BOQ Manager - Installer
; Revit 2023
; ==========================================================

#define MyAppName "RUKN 5D BOQ Manager"
#define MyAppVersion "1.0.0"
#define PublishDir "C:\ProgramData\Autodesk\Revit\Addins\2023"

[Setup]
AppId={{A1F3E8B4-1234-5678-ABCD-987654321000}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={commonappdata}\Autodesk\Revit\Addins\2023\RuknBoqMapper
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=RUKN_5D_BOQ_Manager_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{commonappdata}\Autodesk\Revit\Addins\2023\RuknBoqMapper"

[Files]
; Add-in manifest
Source: "RuknBoqMapper\RuknBoqMapper.addin"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion

; All application files (DLL, PDB, Resources subfolder)
Source: "RuknBoqMapper\bin\Debug\net48\*"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2023\RuknBoqMapper"; Flags: ignoreversion recursesubdirs createallsubdirs

[Code]
var
  HeaderPanel: TPanel;
  HeaderTitleLabel: TLabel;
  HeaderSubtitleLabel: TLabel;
  BodyPanel: TPanel;
  TermsButton: TButton;
  FinishedLabel: TLabel;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure TermsButtonClick(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExec('open', 'https://rukn-bim-website-opka.vercel.app/', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure InitializeWizard();
begin
  // Set form size to match Autodesk installer
  WizardForm.ClientWidth := 500;
  WizardForm.ClientHeight := 360;
  WizardForm.Position := poScreenCenter;

  // Position OuterNotebook below the black header panel
  WizardForm.OuterNotebook.Width := WizardForm.ClientWidth;
  WizardForm.OuterNotebook.Height := WizardForm.ClientHeight - 90 - 60; // Leave 60px for the bottom buttons panel
  WizardForm.OuterNotebook.Left := 0;
  WizardForm.OuterNotebook.Top := 90;

  // 1. Create Black Header Panel
  HeaderPanel := TPanel.Create(WizardForm);
  HeaderPanel.Parent := WizardForm;
  HeaderPanel.Left := 0;
  HeaderPanel.Top := 0;
  HeaderPanel.Width := WizardForm.ClientWidth;
  HeaderPanel.Height := 90;
  HeaderPanel.BevelOuter := bvNone;
  HeaderPanel.Color := clBlack;
  HeaderPanel.ParentBackground := False;

  // Header Title
  HeaderTitleLabel := TLabel.Create(WizardForm);
  HeaderTitleLabel.Parent := HeaderPanel;
  HeaderTitleLabel.Left := 20;
  HeaderTitleLabel.Top := 15;
  HeaderTitleLabel.Caption := 'RUKNBIM';
  HeaderTitleLabel.Font.Name := 'Segoe UI';
  HeaderTitleLabel.Font.Size := 18;
  HeaderTitleLabel.Font.Style := [fsBold];
  HeaderTitleLabel.Font.Color := clWhite;

  // Header Subtitle
  HeaderSubtitleLabel := TLabel.Create(WizardForm);
  HeaderSubtitleLabel.Parent := HeaderPanel;
  HeaderSubtitleLabel.Left := 20;
  HeaderSubtitleLabel.Top := 50;
  HeaderSubtitleLabel.Caption := '{#MyAppName} {#MyAppVersion}';
  HeaderSubtitleLabel.Font.Name := 'Segoe UI';
  HeaderSubtitleLabel.Font.Size := 12;
  HeaderSubtitleLabel.Font.Color := clWhite;

  // 2. Create Custom White Body Panel (used on Ready and Finished pages)
  BodyPanel := TPanel.Create(WizardForm);
  BodyPanel.Parent := WizardForm;
  BodyPanel.Left := 0;
  BodyPanel.Top := HeaderPanel.Height;
  BodyPanel.Width := WizardForm.ClientWidth;
  BodyPanel.Height := WizardForm.ClientHeight - HeaderPanel.Height - 60; // 60 for bottom buttons area
  BodyPanel.BevelOuter := bvNone;
  BodyPanel.Color := clWhite;
  BodyPanel.ParentBackground := False;

  // Hide default Ready to Install components from the standard WizardForm page so they don't leak
  WizardForm.ReadyLabel.Hide;
  WizardForm.ReadyMemo.Hide;

  // 3. Customize the Bottom Buttons Area
  WizardForm.Color := $F0F0F0;

  // Hide the back button completely
  WizardForm.BackButton.Hide;

  // Create "View Terms and Conditions" button on the bottom left
  TermsButton := TButton.Create(WizardForm);
  TermsButton.Parent := WizardForm;
  TermsButton.Caption := 'View Store Terms and Conditions';
  TermsButton.Width := 200;
  TermsButton.Height := WizardForm.CancelButton.Height;
  TermsButton.Left := 20;
  TermsButton.Top := WizardForm.CancelButton.Top;
  TermsButton.OnClick := @TermsButtonClick;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpReady then
  begin
    // Welcome / Ready screen: Show the custom BodyPanel with "Install Now"
    BodyPanel.Show;
    TermsButton.Show;
    WizardForm.OuterNotebook.Hide;

    // Reposition NextButton to act as "Install Now" button
    WizardForm.NextButton.Parent := BodyPanel;
    WizardForm.NextButton.Caption := '   Install Now';
    WizardForm.NextButton.Width := 160;
    WizardForm.NextButton.Height := 40;
    WizardForm.NextButton.Left := (BodyPanel.Width - WizardForm.NextButton.Width) div 2;
    WizardForm.NextButton.Top := (BodyPanel.Height - WizardForm.NextButton.Height) div 2;
    WizardForm.NextButton.ElevationRequired := True; // UAC Shield icon
    WizardForm.NextButton.Show;

    // Set Cancel button position on the bottom right
    WizardForm.CancelButton.Parent := WizardForm;
    WizardForm.CancelButton.Left := WizardForm.ClientWidth - WizardForm.CancelButton.Width - 20;
    WizardForm.CancelButton.Top := WizardForm.ClientHeight - WizardForm.CancelButton.Height - 15;
  end
  else if CurPageID = wpInstalling then
  begin
    // Installation screen: Hide custom BodyPanel, show progress page
    BodyPanel.Hide;
    TermsButton.Hide;
    WizardForm.OuterNotebook.Show;

    // Set page color to White
    WizardForm.InstallingPage.Color := clWhite;
    WizardForm.InnerPage.Color := clWhite;

    // Reposition progress bar and status text onto the white area
    WizardForm.StatusLabel.Parent := WizardForm.InstallingPage;
    WizardForm.StatusLabel.Left := 20;
    WizardForm.StatusLabel.Width := WizardForm.ClientWidth - 40;
    WizardForm.StatusLabel.Top := 30;

    WizardForm.ProgressGauge.Parent := WizardForm.InstallingPage;
    WizardForm.ProgressGauge.Left := 20;
    WizardForm.ProgressGauge.Width := WizardForm.ClientWidth - 40;
    WizardForm.ProgressGauge.Top := 60;
    WizardForm.ProgressGauge.Height := 24;

    // Hide filename label to keep it clean (like Autodesk installer)
    WizardForm.FileNameLabel.Hide;

    // Put NextButton back onto WizardForm and hide it (it is disabled during installation anyway)
    WizardForm.NextButton.Parent := WizardForm;
    WizardForm.NextButton.Hide;
  end
  else if CurPageID = wpFinished then
  begin
    // Finished screen: Custom styled finish page
    BodyPanel.Show;
    TermsButton.Hide;
    WizardForm.OuterNotebook.Hide;

    // Remove UAC Shield from NextButton (which is now "Finish")
    WizardForm.NextButton.Parent := WizardForm;
    WizardForm.NextButton.ElevationRequired := False;
    WizardForm.NextButton.Caption := 'Finish';
    WizardForm.NextButton.Width := 90;
    WizardForm.NextButton.Height := WizardForm.CancelButton.Height;
    WizardForm.NextButton.Left := WizardForm.CancelButton.Left;
    WizardForm.NextButton.Top := WizardForm.CancelButton.Top;
    WizardForm.NextButton.Show;

    // Hide Cancel button since setup is completed
    WizardForm.CancelButton.Hide;

    // Create success message label on the BodyPanel
    if FinishedLabel = nil then
    begin
      FinishedLabel := TLabel.Create(WizardForm);
      FinishedLabel.Parent := BodyPanel;
      FinishedLabel.Left := 20;
      FinishedLabel.Width := BodyPanel.Width - 40;
      FinishedLabel.Height := 80;
      FinishedLabel.AutoSize := False;
      FinishedLabel.WordWrap := True;
      FinishedLabel.Font.Name := 'Segoe UI';
      FinishedLabel.Font.Size := 12;
      FinishedLabel.Font.Color := clBlack;
    end;
    FinishedLabel.Caption := 'Installation completed successfully!' + #13#10#13#10 + '{#MyAppName} is now installed and ready to use in Autodesk Revit.';
    FinishedLabel.Top := (BodyPanel.Height - FinishedLabel.Height) div 2;
  end;
end;
