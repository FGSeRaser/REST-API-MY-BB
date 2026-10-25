''' <summary>Accounts (several forums/users), login, API keys and client settings.</summary>
Friend Class AccountsPage
    Inherits PageBase

    ' Accounts
    Private ReadOnly accountsCard As New CardPanel() With {.Title = "Konten", .Subtitle = "Mehrere Foren und Benutzer – wechseln mit einem Klick"}
    Private ReadOnly accountList As New EntryListBox() With {.ItemHeight = 54, .EmptyText = "Noch kein Konto. Füge unten dein Forum hinzu."}
    Private ReadOnly useButton As ModernButton = MakeButton("Verwenden", ButtonKind.Secondary, Theme.IconForward)
    Private ReadOnly removeButton As ModernButton = MakeButton("Abmelden", ButtonKind.Ghost, Theme.IconDelete)
    Private ReadOnly addButton As ModernButton = MakeButton("Konto hinzufügen", ButtonKind.Primary, Theme.IconAdd)

    ' Login
    Private ReadOnly loginCard As New CardPanel() With {.Title = "Konto hinzufügen"}
    Private ReadOnly urlLabel As Label = MakeLabel("Adresse deines Forums", True, True)
    Private ReadOnly urlBox As New DarkTextBox() With {.Placeholder = "z. B. https://mein-forum.de"}
    Private ReadOnly modeLabel As Label = MakeLabel("Anmelden mit", True, True)
    Private ReadOnly modeCombo As New DarkComboBox()
    Private ReadOnly userLabel As Label = MakeLabel("Benutzername", True, True)
    Private ReadOnly userBox As New DarkTextBox()
    Private ReadOnly passwordLabel As Label = MakeLabel("Passwort", True, True)
    Private ReadOnly passwordBox As New DarkTextBox() With {.UsePassword = True}
    Private ReadOnly keyLabel As Label = MakeLabel("API-Key (im Forum: Benutzer-CP → Forum-Client & API)", True, True)
    Private ReadOnly keyBox As New DarkTextBox() With {.Placeholder = "fgk_…"}
    Private ReadOnly loginInfo As Label = MakeLabel("", True, True)
    Private ReadOnly testButton As ModernButton = MakeButton("Verbindung testen", ButtonKind.Secondary)
    Private ReadOnly connectButton As ModernButton = MakeButton("Anmelden", ButtonKind.Primary, Theme.IconKey)
    Private ReadOnly cancelLoginButton As ModernButton = MakeButton("Abbrechen", ButtonKind.Ghost)

    ' Keys and sessions
    Private ReadOnly keysCard As New CardPanel() With {.Title = "Zugänge & API-Keys", .Subtitle = "Alle Geräte und Keys deines aktiven Kontos"}
    Private ReadOnly keyList As New EntryListBox() With {.ItemHeight = 50, .EmptyText = "Melde dich an, um deine Zugänge zu sehen."}
    Private ReadOnly newKeyButton As ModernButton = MakeButton("Neuer API-Key", ButtonKind.Secondary, Theme.IconKey)
    Private ReadOnly revokeButton As ModernButton = MakeButton("Widerrufen", ButtonKind.Ghost, Theme.IconDelete)

    ' Settings
    Private ReadOnly settingsCard As New CardPanel() With {.Title = "Einstellungen"}
    Private ReadOnly latestLabel As Label = MakeLabel("Neueste Beiträge auf der Startseite", True, True)
    Private ReadOnly latestCombo As New DarkComboBox()
    Private ReadOnly pollLabel As Label = MakeLabel("Auf Neues prüfen", True, True)
    Private ReadOnly pollCombo As New DarkComboBox()
    Private ReadOnly perPageLabel As Label = MakeLabel("Beiträge pro Seite", True, True)
    Private ReadOnly perPageCombo As New DarkComboBox()
    Private ReadOnly notifyToggle As New ToggleSwitch() With {.Text = "Benachrichtigungen bei neuen Beiträgen und PNs"}
    Private ReadOnly trayToggle As New ToggleSwitch() With {.Text = "Beim Schließen im Infobereich weiterlaufen"}
    Private ReadOnly autostartToggle As New ToggleSwitch() With {.Text = "Mit Windows starten"}
    Private ReadOnly signatureToggle As New ToggleSwitch() With {.Text = "Signaturen in Themen anzeigen"}
    Private ReadOnly projectButton As ModernButton = MakeButton("Plugin & Anleitung auf GitHub", ButtonKind.Ghost, Theme.IconOpen)

    Private Shared ReadOnly LatestValues As Integer() = {10, 20, 30, 50}
    Private Shared ReadOnly PollValues As Integer() = {0, 1, 2, 5, 10, 30}
    Private Shared ReadOnly PerPageValues As Integer() = {10, 20, 30, 50}

    Private loginVisible As Boolean
    Private reloginAccount As Account
    Private loadingSettings As Boolean

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Konten & Einstellungen", "Anmeldung, API-Keys und Optionen")

        modeCombo.Items.AddRange(New Object() {"Benutzername und Passwort", "API-Key"})
        modeCombo.SelectedIndex = 0
        latestCombo.Items.AddRange(LatestValues.Select(Function(v) CObj(v & " Einträge")).ToArray())
        pollCombo.Items.AddRange(New Object() {"Aus", "Jede Minute", "Alle 2 Minuten", "Alle 5 Minuten", "Alle 10 Minuten", "Alle 30 Minuten"})
        perPageCombo.Items.AddRange(PerPageValues.Select(Function(v) CObj(v & " Beiträge")).ToArray())
        loginInfo.AutoEllipsis = False

        accountsCard.Controls.AddRange(New Control() {accountList, useButton, removeButton, addButton})
        loginCard.Controls.AddRange(New Control() {urlLabel, urlBox, modeLabel, modeCombo, userLabel, userBox, passwordLabel, passwordBox,
                                                   keyLabel, keyBox, loginInfo, testButton, connectButton, cancelLoginButton})
        keysCard.Controls.AddRange(New Control() {keyList, newKeyButton, revokeButton})
        settingsCard.Controls.AddRange(New Control() {latestLabel, latestCombo, pollLabel, pollCombo, perPageLabel, perPageCombo,
                                                      notifyToggle, trayToggle, autostartToggle, signatureToggle, projectButton})
        Controls.AddRange(New Control() {accountsCard, loginCard, keysCard, settingsCard})

        AddHandler accountList.SelectedIndexChanged, Sub(s, e) UpdateAccountButtons()
        AddHandler accountList.DoubleClick, Sub(s, e) UseSelected()
        AddHandler useButton.Click, Sub(s, e) UseSelected()
        AddHandler removeButton.Click, AddressOf Remove_Click
        AddHandler addButton.Click, Sub(s, e) StartAddAccount()
        AddHandler modeCombo.SelectedIndexChanged, Sub(s, e) UpdateLoginMode()
        AddHandler testButton.Click, AddressOf Test_Click
        AddHandler connectButton.Click, AddressOf Connect_Click
        AddHandler cancelLoginButton.Click,
            Sub(s, e)
                loginVisible = Main.Store.Accounts.Count = 0
                reloginAccount = Nothing
                PerformLayout()
            End Sub
        AddHandler passwordBox.KeyDown,
            Sub(s, e)
                If e.KeyCode = Keys.Enter Then
                    e.SuppressKeyPress = True
                    Connect_Click(connectButton, EventArgs.Empty)
                End If
            End Sub
        AddHandler keyBox.KeyDown,
            Sub(s, e)
                If e.KeyCode = Keys.Enter Then
                    e.SuppressKeyPress = True
                    Connect_Click(connectButton, EventArgs.Empty)
                End If
            End Sub
        AddHandler keyList.SelectedIndexChanged, Sub(s, e) revokeButton.Enabled = keyList.SelectedItem IsNot Nothing
        AddHandler newKeyButton.Click, AddressOf NewKey_Click
        AddHandler revokeButton.Click, AddressOf Revoke_Click
        AddHandler projectButton.Click, Sub(s, e) AppInfo.OpenUrl(AppInfo.ProjectUrl)

        For Each combo As DarkComboBox In New DarkComboBox() {latestCombo, pollCombo, perPageCombo}
            AddHandler combo.SelectedIndexChanged, Sub(s, e) SaveSettings()
        Next
        For Each toggle As ToggleSwitch In New ToggleSwitch() {notifyToggle, trayToggle, signatureToggle}
            AddHandler toggle.CheckedChanged, Sub(s, e) SaveSettings()
        Next
        AddHandler autostartToggle.CheckedChanged,
            Sub(s, e)
                If Not loadingSettings Then AccountStore.StartWithWindows = autostartToggle.Checked
            End Sub

        UpdateLoginMode()
        revokeButton.Enabled = False
    End Sub

    Friend Overrides Sub OnShown()
        loadingSettings = True
        Dim settings = Main.Store.Settings
        latestCombo.SelectedIndex = Math.Max(0, Array.IndexOf(LatestValues, settings.LatestCount))
        pollCombo.SelectedIndex = Math.Max(0, Array.IndexOf(PollValues, settings.PollMinutes))
        perPageCombo.SelectedIndex = Math.Max(0, Array.IndexOf(PerPageValues, settings.PostsPerPage))
        notifyToggle.Checked = settings.Notifications
        trayToggle.Checked = settings.MinimizeToTray
        signatureToggle.Checked = settings.ShowSignatures
        autostartToggle.Checked = AccountStore.StartWithWindows
        loadingSettings = False

        If Main.Store.Accounts.Count = 0 Then loginVisible = True
        FillAccounts()
        LoadKeysAsync()
        PerformLayout()
    End Sub

    Friend Overrides Sub OnAccountChanged()
        FillAccounts()
        keyList.Items.Clear()
    End Sub

    ' ---------- Accounts ----------

    Private Sub FillAccounts()
        accountList.BeginUpdate()
        accountList.Items.Clear()
        For Each account As Account In Main.Store.Accounts
            Dim active = account.Id = Main.Store.ActiveId
            accountList.Items.Add(New ListEntry With {
                .Title = account.Username,
                .Subtitle = If(account.ForumName.Length > 0, account.ForumName & " · ", "") & account.ForumUrl,
                .Detail = If(account.IsKey, "API-Key", "Passwort-Login"),
                .Badge = If(active, "aktiv", ""),
                .BadgeColor = Theme.Good,
                .Tag = account})
        Next
        accountList.EndUpdate()
        UpdateAccountButtons()
    End Sub

    Private Function SelectedAccount() As Account
        Dim entry = TryCast(accountList.SelectedItem, ListEntry)
        Return If(entry Is Nothing, Nothing, TryCast(entry.Tag, Account))
    End Function

    Private Sub UpdateAccountButtons()
        Dim account = SelectedAccount()
        useButton.Enabled = account IsNot Nothing AndAlso account.Id <> Main.Store.ActiveId
        removeButton.Enabled = account IsNot Nothing
    End Sub

    Private Sub UseSelected()
        Dim account = SelectedAccount()
        If account Is Nothing OrElse account.Id = Main.Store.ActiveId Then Return
        Main.SwitchAccount(account)
    End Sub

    Private Async Sub Remove_Click(sender As Object, e As EventArgs)
        Dim account = SelectedAccount()
        If account Is Nothing Then Return
        If MessageBox.Show(Main, "Konto """ & account.DisplayName & """ aus dem Client entfernen?" & Environment.NewLine &
                           If(account.IsKey, "Der API-Key bleibt im Forum gültig, bis du ihn dort widerrufst.", "Die Anmeldung wird im Forum beendet."),
                           "Abmelden", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return

        If Not account.IsKey Then
            Try
                Using client = account.CreateClient()
                    Await client.PostAsync("auth/logout", New Dictionary(Of String, Object)())
                End Using
            Catch
                ' Token may already be invalid
            End Try
        End If
        Dim wasActive = account.Id = Main.Store.ActiveId
        Main.Store.Accounts.Remove(account)
        If wasActive Then Main.Store.ActiveId = If(Main.Store.Accounts.Count > 0, Main.Store.Accounts(0).Id, "")
        Main.Store.Save()
        If wasActive Then
            If Main.Store.Active IsNot Nothing Then
                Main.SwitchAccount(Main.Store.Active)
            Else
                Main.AccountRemoved()
            End If
        End If
        FillAccounts()
        Main.ShowToast("Konto entfernt.", ToastKind.Success)
    End Sub

    ''' <summary>Shows the empty login form.</summary>
    Friend Sub StartAddAccount()
        reloginAccount = Nothing
        loginCard.Title = "Konto hinzufügen"
        loginCard.Subtitle = ""
        urlBox.Text = ""
        userBox.Text = ""
        passwordBox.Text = ""
        keyBox.Text = ""
        loginInfo.Text = "Das Forum braucht das Plugin ""FG Forum API"". Es gibt es kostenlos auf GitHub."
        loginInfo.ForeColor = Theme.TextMuted
        loginVisible = True
        PerformLayout()
        urlBox.Focus()
    End Sub

    ''' <summary>Login form prefilled after an expired login.</summary>
    Friend Sub PrepareRelogin(account As Account)
        If account Is Nothing Then
            StartAddAccount()
            Return
        End If
        reloginAccount = account
        loginCard.Title = "Neu anmelden"
        loginCard.Subtitle = account.DisplayName
        urlBox.Text = account.ForumUrl
        userBox.Text = account.Username
        passwordBox.Text = ""
        keyBox.Text = ""
        modeCombo.SelectedIndex = If(account.IsKey, 1, 0)
        loginInfo.Text = "Die Anmeldung ist abgelaufen oder wurde widerrufen."
        loginInfo.ForeColor = Theme.Warning
        loginVisible = True
        PerformLayout()
        If account.IsKey Then keyBox.Focus() Else passwordBox.Focus()
    End Sub

    Private Sub UpdateLoginMode()
        Dim useKey = modeCombo.SelectedIndex = 1
        userLabel.Visible = Not useKey
        userBox.Visible = Not useKey
        passwordLabel.Visible = Not useKey
        passwordBox.Visible = Not useKey
        keyLabel.Visible = useKey
        keyBox.Visible = useKey
        PerformLayout()
    End Sub

    Private Async Sub Test_Click(sender As Object, e As EventArgs)
        Dim url = ApiClient.NormalizeForumUrl(urlBox.Text)
        If url.Length = 0 Then
            ShowLoginInfo("Bitte gib die Adresse deines Forums ein.", Theme.Warning)
            Return
        End If
        Await RunBusy(testButton,
            Async Function()
                Using client As New ApiClient(url)
                    Try
                        Dim info = Await client.GetAsync("info")
                        Dim board = Json.GetObject(info, "board")
                        Dim login = Json.GetObject(info, "login")
                        Dim found = "✓ Gefunden: " & TextUtil.Decode(Json.GetString(board, "name")) & " (MyBB " & Json.GetString(info, "mybb_version") &
                                    ", API " & Json.GetString(info, "plugin_version") & ")"
                        If Not Json.GetBool(login, "password", True) Then found &= Environment.NewLine & "Dieses Forum erlaubt nur den Login mit API-Key."
                        ShowLoginInfo(found, Theme.Good)
                    Catch ex As ApiException
                        ShowLoginInfo(ex.Message, Theme.Danger)
                    End Try
                End Using
            End Function)
    End Sub

    Private Sub ShowLoginInfo(message As String, textColor As Color)
        loginInfo.Text = message
        loginInfo.ForeColor = textColor
    End Sub

    Private Async Sub Connect_Click(sender As Object, e As EventArgs)
        Dim url = ApiClient.NormalizeForumUrl(urlBox.Text)
        Dim useKey = modeCombo.SelectedIndex = 1
        If url.Length = 0 Then
            ShowLoginInfo("Bitte gib die Adresse deines Forums ein.", Theme.Warning)
            urlBox.Focus()
            Return
        End If
        If useKey AndAlso keyBox.Text.Trim().Length = 0 Then
            ShowLoginInfo("Bitte füge deinen API-Key ein.", Theme.Warning)
            keyBox.Focus()
            Return
        End If
        If Not useKey AndAlso (userBox.Text.Trim().Length = 0 OrElse passwordBox.Text.Length = 0) Then
            ShowLoginInfo("Bitte gib Benutzername und Passwort ein.", Theme.Warning)
            Return
        End If

        Await RunBusy(connectButton,
            Async Function()
                ShowLoginInfo("Verbinde …", Theme.TextMuted)
                Using client As New ApiClient(url)
                    Try
                        Dim info = Await client.GetAsync("info")
                        Dim boardName = TextUtil.Decode(Json.GetString(Json.GetObject(info, "board"), "name"))
                        Dim secret As String
                        Dim userInfo As MeInfo
                        If useKey Then
                            secret = keyBox.Text.Trim()
                            client.Secret = secret
                            client.SecretIsKey = True
                            userInfo = MeInfo.From(Await client.GetAsync("me"))
                        Else
                            Dim result = Await client.PostAsync("auth/login", New Dictionary(Of String, Object) From {
                                {"username", userBox.Text.Trim()}, {"password", passwordBox.Text},
                                {"client", AppInfo.AppName & " (" & Environment.MachineName & ")"}})
                            secret = Json.GetString(result, "token")
                            userInfo = MeInfo.From(Json.GetObject(result, "user"))
                        End If

                        ' Same forum and user again: update instead of adding twice
                        Dim account = Main.Store.Accounts.FirstOrDefault(Function(a) a.ForumUrl = client.ForumUrl AndAlso a.Uid = userInfo.User.Uid)
                        If account Is Nothing AndAlso reloginAccount IsNot Nothing AndAlso reloginAccount.ForumUrl = client.ForumUrl Then account = reloginAccount
                        If account Is Nothing Then
                            account = New Account()
                            Main.Store.Accounts.Add(account)
                        End If
                        account.ForumUrl = client.ForumUrl
                        account.ForumName = boardName
                        account.Username = userInfo.User.Username
                        account.Uid = userInfo.User.Uid
                        account.Avatar = userInfo.User.Avatar
                        account.Kind = If(useKey, "key", "session")
                        account.Secret = secret
                        Main.Store.ActiveId = account.Id
                        Main.Store.Save()

                        passwordBox.Text = ""
                        keyBox.Text = ""
                        loginVisible = False
                        reloginAccount = Nothing
                        ShowLoginInfo("", Theme.TextMuted)
                        FillAccounts()
                        PerformLayout()
                        If Await Main.ConnectAsync(account) Then
                            Main.ShowToast("Angemeldet als " & account.Username & " bei " & boardName & ".", ToastKind.Success)
                            Main.ShowPage(Main.HomeView)
                        End If
                    Catch ex As ApiException
                        ShowLoginInfo(ex.FullMessage, Theme.Danger)
                    End Try
                End Using
            End Function)
    End Sub

    ' ---------- Keys and sessions ----------

    Private Async Sub LoadKeysAsync()
        If Not IsConnected Then
            keyList.Items.Clear()
            newKeyButton.Enabled = False
            Return
        End If
        Try
            Dim data = Await Api.GetAsync("keys")
            keyList.BeginUpdate()
            keyList.Items.Clear()
            For Each item As Object In Json.GetArray(data, "items")
                Dim access = AccessEntry.From(item)
                keyList.Items.Add(New ListEntry With {
                    .Title = access.Label,
                    .Subtitle = If(access.Kind = "key", "API-Key", "Client-Anmeldung") & " · …" & access.Hint & If(access.Ip.Length > 0, " · " & access.Ip, ""),
                    .Detail = If(access.LastUsed > 0, Fmt.Relative(access.LastUsed), "noch nie benutzt"),
                    .Badge = If(access.IsCurrent, "dieses Gerät", ""),
                    .BadgeColor = Theme.Good,
                    .Tag = access})
            Next
            keyList.EndUpdate()
            newKeyButton.Enabled = Json.GetBool(data, "can_create") AndAlso Main.Store.Active IsNot Nothing AndAlso Not Main.Store.Active.IsKey
        Catch ex As Exception
            Main.ShowError(ex)
        End Try
    End Sub

    Private Async Sub NewKey_Click(sender As Object, e As EventArgs)
        Dim keyName = InputDialog.Ask(Main, "Neuer API-Key", "Name für den Key (damit du später weißt, wofür er ist):", "Mein Bot")
        If String.IsNullOrWhiteSpace(keyName) Then Return
        Await RunBusy(newKeyButton,
            Async Function()
                Dim result = Await Api.PostAsync("keys/create", New Dictionary(Of String, Object) From {{"label", keyName.Trim()}})
                Using dialog As New SecretDialog("API-Key erstellt",
                                                 "Kopiere den Key jetzt. Er wird aus Sicherheitsgründen nur dieses eine Mal angezeigt." & Environment.NewLine &
                                                 "Senden im Header ""X-FG-Key"" oder im Client unter ""Anmelden mit: API-Key"".",
                                                 Json.GetString(result, "key"))
                    dialog.ShowDialog(Main)
                End Using
                LoadKeysAsync()
            End Function)
    End Sub

    Private Async Sub Revoke_Click(sender As Object, e As EventArgs)
        Dim entry = TryCast(keyList.SelectedItem, ListEntry)
        If entry Is Nothing Then Return
        Dim access = DirectCast(entry.Tag, AccessEntry)
        Dim question = If(access.IsCurrent,
                          "Das ist die Anmeldung dieses Clients. Danach musst du dich neu anmelden. Fortfahren?",
                          """" & access.Label & """ widerrufen? Programme mit diesem Zugang können danach nicht mehr auf das Forum zugreifen.")
        If MessageBox.Show(Main, question, "Zugang widerrufen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
        Await RunBusy(revokeButton,
            Async Function()
                Await Api.PostAsync("keys/revoke", New Dictionary(Of String, Object) From {{"id", access.Id}})
                Main.ShowToast("Zugang widerrufen.", ToastKind.Success)
                If access.IsCurrent Then
                    Main.SetStatus("Abgemeldet", Theme.TextDim)
                    PrepareRelogin(Main.Store.Active)
                Else
                    LoadKeysAsync()
                End If
            End Function)
    End Sub

    ' ---------- Settings ----------

    Private Sub SaveSettings()
        If loadingSettings Then Return
        Dim settings = Main.Store.Settings
        If latestCombo.SelectedIndex >= 0 Then settings.LatestCount = LatestValues(latestCombo.SelectedIndex)
        If pollCombo.SelectedIndex >= 0 Then settings.PollMinutes = PollValues(pollCombo.SelectedIndex)
        If perPageCombo.SelectedIndex >= 0 Then settings.PostsPerPage = PerPageValues(perPageCombo.SelectedIndex)
        settings.Notifications = notifyToggle.Checked
        settings.MinimizeToTray = trayToggle.Checked
        settings.ShowSignatures = signatureToggle.Checked
        Main.Store.Save()
        Main.ApplyPollInterval()
        Main.SetStatus(If(IsConnected, "Verbunden", "Nicht verbunden"), If(IsConnected, Theme.Good, Theme.TextDim))
    End Sub

    ' ---------- Layout ----------

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If settingsCard Is Nothing OrElse projectButton Is Nothing Then Return

        Dim top = HeaderHeight
        Dim total = ClientSize.Width - Pad * 2
        Dim leftWidth = CInt((total - 12) * 0.5)
        Dim rightLeft = Pad + leftWidth + 12
        Dim rightWidth = total - leftWidth - 12
        Dim areaBottom = ClientSize.Height - Pad

        ' Left: accounts + login
        Dim loginHeight = If(loginVisible, 330, 0)
        accountsCard.SetBounds(Pad, top, leftWidth, Math.Max(180, areaBottom - top - If(loginVisible, loginHeight + 12, 0)))
        Dim ah = accountsCard.Height
        accountList.SetBounds(10, accountsCard.ContentTop, leftWidth - 20, ah - accountsCard.ContentTop - 56)
        addButton.SetBounds(16, ah - 46, 170, 34)
        removeButton.SetBounds(leftWidth - 16 - 120, ah - 46, 120, 34)
        useButton.SetBounds(removeButton.Left - 8 - 130, ah - 46, 130, 34)

        loginCard.Visible = loginVisible
        If loginVisible Then
            loginCard.SetBounds(Pad, accountsCard.Bottom + 12, leftWidth, loginHeight)
            Dim w = leftWidth - 32
            Dim y = loginCard.ContentTop
            urlLabel.SetBounds(16, y, w, 18)
            urlBox.SetBounds(16, y + 20, w, 34)
            y += 62
            Dim half = (w - 12) \ 2
            modeLabel.SetBounds(16, y, w, 18)
            modeCombo.SetBounds(16, y + 20, w, 32)
            y += 60
            userLabel.SetBounds(16, y, half, 18)
            userBox.SetBounds(16, y + 20, half, 34)
            passwordLabel.SetBounds(16 + half + 12, y, half, 18)
            passwordBox.SetBounds(16 + half + 12, y + 20, half, 34)
            keyLabel.SetBounds(16, y, w, 18)
            keyBox.SetBounds(16, y + 20, w, 34)
            y += 62
            loginInfo.SetBounds(16, y, w, 40)
            connectButton.SetBounds(leftWidth - 16 - 140, loginHeight - 50, 140, 36)
            testButton.SetBounds(connectButton.Left - 8 - 160, loginHeight - 50, 160, 36)
            cancelLoginButton.SetBounds(16, loginHeight - 50, 110, 36)
            cancelLoginButton.Visible = Main.Store.Accounts.Count > 0
        End If

        ' Right: keys + settings
        Dim settingsHeight = 330
        keysCard.SetBounds(rightLeft, top, rightWidth, Math.Max(160, areaBottom - top - settingsHeight - 12))
        Dim kh = keysCard.Height
        keyList.SetBounds(10, keysCard.ContentTop, rightWidth - 20, kh - keysCard.ContentTop - 56)
        newKeyButton.SetBounds(16, kh - 46, 160, 34)
        revokeButton.SetBounds(rightWidth - 16 - 130, kh - 46, 130, 34)

        settingsCard.SetBounds(rightLeft, keysCard.Bottom + 12, rightWidth, settingsHeight)
        Dim sw = rightWidth - 32
        Dim third = (sw - 24) \ 3
        Dim sy = settingsCard.ContentTop
        latestLabel.SetBounds(16, sy, third, 18)
        latestCombo.SetBounds(16, sy + 20, third, 32)
        pollLabel.SetBounds(16 + third + 12, sy, third, 18)
        pollCombo.SetBounds(16 + third + 12, sy + 20, third, 32)
        perPageLabel.SetBounds(16 + (third + 12) * 2, sy, third, 18)
        perPageCombo.SetBounds(16 + (third + 12) * 2, sy + 20, third, 32)
        sy += 70
        notifyToggle.SetBounds(16, sy, sw, 24)
        trayToggle.SetBounds(16, sy + 34, sw, 24)
        autostartToggle.SetBounds(16, sy + 68, sw, 24)
        signatureToggle.SetBounds(16, sy + 102, sw, 24)
        projectButton.SetBounds(16, settingsHeight - 48, 260, 34)
    End Sub

End Class
