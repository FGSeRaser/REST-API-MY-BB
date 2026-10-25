''' <summary>Main window: sidebar, page navigation, current account, tray icon and notifications.</summary>
Friend Class MainForm
    Inherits Form

    Friend ReadOnly Store As AccountStore = AccountStore.Load()
    Friend ReadOnly Images As New AttachmentCache()

    ''' <summary>API of the active account (Nothing while not logged in).</summary>
    Friend Property Api As ApiClient
    ''' <summary>Info about the logged in user (Nothing while not logged in).</summary>
    Friend Property CurrentUser As MeInfo

    Friend ReadOnly HomeView As HomePage
    Friend ReadOnly ForumsView As ForumsPage
    Friend ReadOnly ThreadView As ThreadPage
    Friend ReadOnly EditorView As EditorPage
    Friend ReadOnly MessagesView As MessagesPage
    Friend ReadOnly SearchView As SearchPage
    Friend ReadOnly AccountsView As AccountsPage

    Private ReadOnly sidebar As Panel
    Private ReadOnly content As Panel
    Private ReadOnly accountTile As New AccountCard()
    Private ReadOnly authorLink As New GitHubLink()
    Private ReadOnly navButtons As New List(Of NavButton)()
    Private ReadOnly navMessages As NavButton
    Private ReadOnly pages As New List(Of PageBase)()
    Private ReadOnly history As New Stack(Of PageBase)()
    Private ReadOnly toast As New ToastBar()
    Private ReadOnly tray As New NotifyIcon()
    Private ReadOnly pollTimer As New Timer()
    Private ReadOnly startHidden As Boolean

    Private currentPage As PageBase
    Private exitRequested As Boolean
    Private trayHintShown As Boolean
    Private polling As Boolean
    Private balloonTarget As String = ""
    Private statusText As String = "Nicht verbunden"
    Private statusColor As Color = Theme.TextDim

    Public Sub New(Optional startInTray As Boolean = False)
        startHidden = startInTray
        Text = AppInfo.AppName
        StartPosition = FormStartPosition.CenterScreen
        AutoScaleMode = AutoScaleMode.Dpi
        ClientSize = New System.Drawing.Size(1260, 800)
        MinimumSize = New System.Drawing.Size(1080, 720)
        BackColor = Theme.Back
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
        KeyPreview = True
        Try
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
        End Try

        sidebar = New DoubleBufferedPanel() With {.Dock = DockStyle.Left, .Width = 236, .BackColor = Theme.Sidebar}
        AddHandler sidebar.Paint, AddressOf Sidebar_Paint
        content = New Panel() With {.Dock = DockStyle.Fill, .BackColor = Theme.Back}

        accountTile.SetBounds(16, 88, 204, 58)
        AddHandler accountTile.Click, AddressOf AccountCard_Click
        sidebar.Controls.Add(accountTile)
        sidebar.Controls.Add(authorLink)
        AddHandler sidebar.Resize, Sub(s, e) authorLink.SetBounds(16, sidebar.Height - 92 - 54, sidebar.Width - 32, 46)

        HomeView = New HomePage(Me)
        ForumsView = New ForumsPage(Me)
        ThreadView = New ThreadPage(Me)
        EditorView = New EditorPage(Me)
        MessagesView = New MessagesPage(Me)
        SearchView = New SearchPage(Me)
        AccountsView = New AccountsPage(Me)

        AddNav(HomeView, "Start", Theme.IconHome)
        AddNav(ForumsView, "Foren", Theme.IconForum)
        navMessages = AddNav(MessagesView, "Nachrichten", Theme.IconMail)
        AddNav(SearchView, "Suche", Theme.IconSearch)
        AddNav(AccountsView, "Konten & Einstellungen", Theme.IconAccount)
        AddPage(ThreadView)
        AddPage(EditorView)

        content.Controls.Add(toast)
        Controls.Add(content)
        Controls.Add(sidebar)

        SetupTray()
        AddHandler pollTimer.Tick, AddressOf PollTimer_Tick
    End Sub

    ' ---------- Startup / shutdown ----------

    Protected Overrides Sub SetVisibleCore(value As Boolean)
        If startHidden AndAlso Not IsHandleCreated Then
            CreateHandle()
            value = False
        End If
        MyBase.SetVisibleCore(value)
    End Sub

    Protected Overrides Async Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Theme.UseDarkTitleBar(Me)
        If Store.Active Is Nothing Then
            ShowPage(AccountsView)
        Else
            ShowPage(HomeView)
            Await ConnectAsync(Store.Active)
        End If
        ApplyPollInterval()
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If Not exitRequested AndAlso Store.Settings.MinimizeToTray AndAlso e.CloseReason = CloseReason.UserClosing Then
            e.Cancel = True
            EditorView.SaveDraftNow()
            Hide()
            If Not trayHintShown Then
                trayHintShown = True
                tray.ShowBalloonTip(3000, AppInfo.AppName & " läuft weiter",
                                    "Du wirst über neue Beiträge und Nachrichten informiert. Beenden über das Symbol im Infobereich.", ToolTipIcon.Info)
            End If
            Return
        End If
        EditorView.SaveDraftNow()
        pollTimer.Stop()
        tray.Visible = False
        tray.Dispose()
        Store.Save()
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub SetupTray()
        tray.Icon = If(Icon, SystemIcons.Application)
        tray.Text = AppInfo.AppName
        tray.Visible = True
        Dim menu As New ContextMenuStrip()
        menu.Items.Add("Öffnen", Nothing, Sub(s, e) ShowFromTray())
        menu.Items.Add("Neueste Beiträge", Nothing,
            Sub(s, e)
                ShowFromTray()
                ShowPage(HomeView)
            End Sub)
        menu.Items.Add("Nachrichten", Nothing,
            Sub(s, e)
                ShowFromTray()
                ShowPage(MessagesView)
            End Sub)
        menu.Items.Add(New ToolStripSeparator())
        menu.Items.Add("Beenden", Nothing,
            Sub(s, e)
                exitRequested = True
                Close()
            End Sub)
        tray.ContextMenuStrip = menu
        AddHandler tray.DoubleClick, Sub(s, e) ShowFromTray()
        AddHandler tray.BalloonTipClicked, AddressOf Tray_BalloonTipClicked
    End Sub

    Friend Sub ShowFromTray()
        Show()
        If WindowState = FormWindowState.Minimized Then WindowState = FormWindowState.Normal
        Activate()
    End Sub

    Private Sub Tray_BalloonTipClicked(sender As Object, e As EventArgs)
        ShowFromTray()
        If balloonTarget = "messages" Then
            ShowPage(MessagesView)
        ElseIf balloonTarget = "latest" Then
            ShowPage(HomeView)
        End If
    End Sub

    ' ---------- Keyboard shortcuts ----------

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        Select Case keyData
            Case Keys.F5
                If currentPage IsNot Nothing Then currentPage.RefreshPage()
                Return True
            Case Keys.Alt Or Keys.Left, Keys.BrowserBack
                GoBack()
                Return True
            Case Keys.Control Or Keys.F
                ShowPage(SearchView)
                Return True
        End Select
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    ' ---------- Navigation ----------

    Private Function AddNav(page As PageBase, title As String, glyph As String) As NavButton
        AddPage(page)
        Dim nav As New NavButton() With {
            .Text = title, .IconGlyph = glyph, .Tag = page,
            .Location = New Point(16, 164 + navButtons.Count * 46), .Size = New System.Drawing.Size(204, 40)}
        AddHandler nav.Click, Sub(s, e) ShowPage(DirectCast(DirectCast(s, NavButton).Tag, PageBase), False)
        sidebar.Controls.Add(nav)
        navButtons.Add(nav)
        Return nav
    End Function

    Private Sub AddPage(page As PageBase)
        page.Dock = DockStyle.Fill
        page.Visible = False
        content.Controls.Add(page)
        pages.Add(page)
    End Sub

    ''' <summary>Shows a page. With remember=True, "Zurück" returns to the current page.</summary>
    Friend Sub ShowPage(page As PageBase, Optional remember As Boolean = False)
        If page Is Nothing Then Return
        If remember AndAlso currentPage IsNot Nothing AndAlso currentPage IsNot page Then
            history.Push(currentPage)
        ElseIf Not remember Then
            history.Clear()
        End If
        DisplayPage(page)
    End Sub

    Private Sub DisplayPage(page As PageBase)
        If currentPage Is EditorView AndAlso page IsNot EditorView Then EditorView.SaveDraftNow()

        currentPage = page
        For Each p As PageBase In pages
            p.Visible = p Is page
        Next
        Dim navTarget = If(page Is ThreadView, ForumsView, If(page Is EditorView, EditorView.OriginPage, page))
        For Each nav As NavButton In navButtons
            nav.Selected = nav.Tag Is navTarget
        Next
        toast.BringToFront()
        page.OnShown()
    End Sub

    Friend ReadOnly Property CanGoBack As Boolean
        Get
            Return history.Count > 0
        End Get
    End Property

    Friend Sub GoBack()
        If history.Count = 0 Then
            If currentPage Is ThreadView OrElse currentPage Is EditorView Then ShowPage(ForumsView)
            Return
        End If
        DisplayPage(history.Pop())
    End Sub

    Friend Sub OpenThread(tid As Integer, Optional pid As Integer = 0, Optional lastPage As Boolean = False)
        ShowPage(ThreadView, True)
        ThreadView.LoadThread(tid, pid, lastPage)
    End Sub

    Friend Sub OpenForum(fid As Integer)
        ShowPage(ForumsView)
        ForumsView.SelectForum(fid)
    End Sub

    Friend Sub OpenNewThread(fid As Integer)
        EditorView.StartNewThread(fid, currentPage)
        ShowPage(EditorView, True)
    End Sub

    Friend Sub OpenReply(thread As ThreadInfo, Optional initialText As String = "")
        EditorView.StartReply(thread, initialText, currentPage)
        ShowPage(EditorView, True)
    End Sub

    Friend Sub OpenEdit(pid As Integer)
        EditorView.StartEdit(pid, currentPage)
        ShowPage(EditorView, True)
    End Sub

    Friend Sub OpenNewMessage(Optional recipient As String = "", Optional subject As String = "", Optional text As String = "", Optional replyTo As Integer = 0)
        EditorView.StartMessage(recipient, subject, text, replyTo, currentPage)
        ShowPage(EditorView, True)
    End Sub

    Friend Async Sub OpenUser(uid As Integer)
        If Api Is Nothing OrElse uid <= 0 Then Return
        Try
            Dim data = Await Api.GetAsync("user", New Dictionary(Of String, Object) From {{"uid", uid}})
            Using dialog As New UserDialog(Me, data)
                dialog.ShowDialog(Me)
            End Using
        Catch ex As Exception
            ShowError(ex)
        End Try
    End Sub

    ' ---------- Accounts ----------

    ''' <summary>Logs in with a saved account and loads the user data.</summary>
    Friend Async Function ConnectAsync(account As Account) As Task(Of Boolean)
        If account Is Nothing Then Return False
        If Api IsNot Nothing Then Api.Dispose()
        Api = account.CreateClient()
        Images.Clear()
        CurrentUser = Nothing
        Store.ActiveId = account.Id
        Store.Save()
        SetStatus("Verbinde …", Theme.Warning)
        accountTile.SetAccount(account, Nothing)

        Try
            Dim data = Await Api.GetAsync("me")
            CurrentUser = MeInfo.From(data)
            account.Username = CurrentUser.User.Username
            account.Uid = CurrentUser.User.Uid
            account.Avatar = CurrentUser.User.Avatar
            Store.Save()
            SetStatus("Verbunden", Theme.Good)
            UpdateUnread(CurrentUser.UnreadPms)
            Dim avatarImage = Await LoadAvatarAsync(account.Avatar)
            accountTile.SetAccount(account, avatarImage)
            For Each page As PageBase In pages
                page.OnAccountChanged()
            Next
            If currentPage IsNot Nothing AndAlso currentPage IsNot AccountsView Then currentPage.OnShown()
            Return True
        Catch ex As ApiException
            SetStatus("Nicht verbunden", Theme.Danger)
            If ex.IsAuthProblem Then
                ShowToast("Die Anmeldung bei " & account.DisplayName & " ist abgelaufen. Bitte melde dich neu an.", ToastKind.Danger)
                ShowPage(AccountsView)
                AccountsView.PrepareRelogin(account)
            Else
                ShowToast(ex.Message, ToastKind.Danger)
            End If
            Return False
        Catch ex As Exception
            SetStatus("Nicht verbunden", Theme.Danger)
            ShowToast("Fehler beim Verbinden: " & ex.Message, ToastKind.Danger)
            Return False
        End Try
    End Function

    Private Async Function LoadAvatarAsync(url As String) As Task(Of Image)
        If String.IsNullOrEmpty(url) OrElse Api Is Nothing Then Return Nothing
        Dim data = Await Api.DownloadPublicAsync(url)
        If data Is Nothing Then Return Nothing
        Try
            Using stream As New MemoryStream(data)
                Using loaded = Image.FromStream(stream)
                    Return New Bitmap(loaded)
                End Using
            End Using
        Catch
            Return Nothing
        End Try
    End Function

    Friend Async Sub SwitchAccount(account As Account)
        Await ConnectAsync(account)
        If CurrentUser IsNot Nothing Then ShowPage(HomeView)
    End Sub

    Private Sub AccountCard_Click(sender As Object, e As EventArgs)
        Dim menu As New ContextMenuStrip() With {.ShowImageMargin = False}
        For Each account As Account In Store.Accounts
            Dim item As New ToolStripMenuItem(account.DisplayName) With {.Checked = account.Id = Store.ActiveId, .Tag = account}
            AddHandler item.Click, Sub(s, args) SwitchAccount(DirectCast(DirectCast(s, ToolStripMenuItem).Tag, Account))
            menu.Items.Add(item)
        Next
        If Store.Accounts.Count > 0 Then menu.Items.Add(New ToolStripSeparator())
        menu.Items.Add("Konto hinzufügen …", Nothing,
            Sub(s, args)
                ShowPage(AccountsView)
                AccountsView.StartAddAccount()
            End Sub)
        menu.Show(accountTile, New Point(0, accountTile.Height))
    End Sub

    Friend Sub AccountRemoved()
        If Store.Active Is Nothing Then
            If Api IsNot Nothing Then Api.Dispose()
            Api = Nothing
            CurrentUser = Nothing
            accountTile.SetAccount(Nothing, Nothing)
            SetStatus("Nicht verbunden", Theme.TextDim)
            UpdateUnread(0)
            For Each page As PageBase In pages
                page.OnAccountChanged()
            Next
        End If
    End Sub

    ' ---------- Status, toasts, errors ----------

    Friend Sub SetStatus(text As String, dotColor As Color)
        statusText = text
        statusColor = dotColor
        sidebar.Invalidate()
    End Sub

    Friend Sub UpdateUnread(count As Integer)
        navMessages.BadgeText = If(count > 0, count.ToString(), "")
        If CurrentUser IsNot Nothing Then CurrentUser.UnreadPms = count
        tray.Text = AppInfo.AppName & If(count > 0, " · " & count & " neue Nachricht(en)", "")
    End Sub

    Friend Sub ShowToast(text As String, Optional kind As ToastKind = ToastKind.Info)
        toast.ShowMessage(text, kind)
    End Sub

    ''' <summary>Shows any error in a friendly way. Lost logins send the user to the accounts page.</summary>
    Friend Sub ShowError(ex As Exception)
        Dim apiError = TryCast(ex, ApiException)
        If apiError Is Nothing Then
            ShowToast("Fehler: " & ex.Message, ToastKind.Danger)
            Return
        End If
        If apiError.IsAuthProblem Then
            SetStatus("Anmeldung abgelaufen", Theme.Danger)
            ShowToast(apiError.Message, ToastKind.Danger)
            ShowPage(AccountsView)
            AccountsView.PrepareRelogin(Store.Active)
            Return
        End If
        If apiError.Details.Count > 0 Then
            MessageBox.Show(Me, apiError.FullMessage, AppInfo.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Else
            ShowToast(apiError.Message, ToastKind.Danger)
        End If
    End Sub

    ' ---------- Notifications ----------

    Friend Sub ApplyPollInterval()
        pollTimer.Stop()
        If Store.Settings.PollMinutes > 0 Then
            pollTimer.Interval = Store.Settings.PollMinutes * 60 * 1000
            pollTimer.Start()
        End If
    End Sub

    Private Async Sub PollTimer_Tick(sender As Object, e As EventArgs)
        If polling OrElse Api Is Nothing OrElse CurrentUser Is Nothing Then Return
        Dim account = Store.Active
        If account Is Nothing Then Return
        polling = True
        Try
            Dim userInfo = MeInfo.From(Await Api.GetAsync("me"))
            UpdateUnread(userInfo.UnreadPms)
            If userInfo.UnreadPms > account.LastUnreadPms AndAlso Store.Settings.Notifications Then
                balloonTarget = "messages"
                tray.ShowBalloonTip(5000, "Neue private Nachricht", "Du hast " & userInfo.UnreadPms & " ungelesene Nachricht(en) in " & account.ForumName & ".", ToolTipIcon.Info)
            End If
            account.LastUnreadPms = userInfo.UnreadPms

            Dim latest = Await Api.GetAsync("latest", New Dictionary(Of String, Object) From {{"limit", 10}})
            Dim items = Json.GetArray(latest, "items").Select(Function(i) PostListItem.From(i)).ToList()
            Dim newest = If(items.Count > 0, items.Max(Function(i) i.Pid), 0)
            If account.LastSeenPid > 0 AndAlso newest > account.LastSeenPid Then
                Dim fresh = items.Where(Function(i) i.Pid > account.LastSeenPid AndAlso i.Author.Uid <> CurrentUser.User.Uid).ToList()
                If fresh.Count > 0 AndAlso Store.Settings.Notifications Then
                    balloonTarget = "latest"
                    Dim first = fresh(0)
                    Dim body = first.Author.Username & " in """ & first.ThreadSubject & """"
                    If fresh.Count > 1 Then body &= Environment.NewLine & "und " & (fresh.Count - 1) & " weitere(r) Beitrag/Beiträge"
                    tray.ShowBalloonTip(5000, If(fresh.Count = 1, "Neuer Beitrag", fresh.Count & " neue Beiträge"), body, ToolTipIcon.Info)
                End If
                HomeView.MarkNewPosts(account.LastSeenPid)
            End If
            If newest > 0 Then account.LastSeenPid = Math.Max(account.LastSeenPid, newest)
            Store.Save()
            SetStatus("Verbunden · " & DateTime.Now.ToString("HH:mm"), Theme.Good)
        Catch ex As ApiException
            SetStatus(If(ex.IsAuthProblem, "Anmeldung abgelaufen", "Keine Verbindung"), Theme.Danger)
        Catch
        Finally
            polling = False
        End Try
    End Sub

    ' ---------- Sidebar ----------

    Private Sub Sidebar_Paint(sender As Object, e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias

        ' Logo: two speech bubbles
        Theme.FillRounded(g, New RectangleF(22, 26, 40, 40), 10, Theme.Accent)
        Theme.FillRounded(g, New RectangleF(29, 34, 20, 14), 4, Color.White)
        Theme.FillRounded(g, New RectangleF(36, 44, 20, 14), 4, Theme.Blend(Theme.Accent, Color.White, 0.55))
        TextRenderer.DrawText(g, "FG Forum Client", Theme.HeadingFont, New Point(74, 28), Theme.TextMain, TextFormatFlags.NoPadding)
        TextRenderer.DrawText(g, "MyBB ohne Browser", Theme.SmallFont, New Point(75, 50), Theme.TextMuted, TextFormatFlags.NoPadding)

        Dim box As New RectangleF(16, sidebar.Height - 92, sidebar.Width - 32, 74)
        Theme.FillRounded(g, box, 8, Theme.Surface, Theme.Border)
        Using dot As New SolidBrush(statusColor)
            g.FillEllipse(dot, box.X + 14, box.Y + 18, 9, 9)
        End Using
        TextRenderer.DrawText(g, statusText, Theme.BodyBoldFont, New Rectangle(CInt(box.X) + 30, CInt(box.Y) + 12, CInt(box.Width) - 40, 20), Theme.TextMain,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        Dim info = If(Store.Settings.PollMinutes > 0, "Prüft alle " & Store.Settings.PollMinutes & " Min. auf Neues", "Automatische Prüfung aus")
        TextRenderer.DrawText(g, info, Theme.SmallFont, New Rectangle(CInt(box.X) + 14, CInt(box.Y) + 42, CInt(box.Width) - 24, 20), Theme.TextMuted,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
    End Sub

End Class


''' <summary>Panel without flicker for custom-painted areas.</summary>
Friend Class DoubleBufferedPanel
    Inherits Panel

    Public Sub New()
        DoubleBuffered = True
        ResizeRedraw = True
    End Sub
End Class


''' <summary>Current account in the sidebar (click opens the account menu).</summary>
Friend Class AccountCard
    Inherits PaintedControl

    Private avatar As Image
    Private title As String = "Nicht angemeldet"
    Private subtitle As String = "Konto hinzufügen"

    Public Sub New()
        SetStyle(ControlStyles.StandardClick, True)
        BackColor = Theme.Sidebar
        Cursor = Cursors.Hand
    End Sub

    Public Sub SetAccount(account As Account, avatarImage As Image)
        avatar = avatarImage
        If account Is Nothing Then
            title = "Nicht angemeldet"
            subtitle = "Konto hinzufügen"
        Else
            title = account.Username
            subtitle = If(account.ForumName.Length > 0, account.ForumName, account.ForumUrl)
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic
        Theme.FillRounded(g, New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F), 10, If(hovering, Theme.SurfaceHover, Theme.Surface), Theme.Border)

        Dim circle As New Rectangle(10, (Height - 36) \ 2, 36, 36)
        If avatar IsNot Nothing Then
            Using path As New System.Drawing.Drawing2D.GraphicsPath()
                path.AddEllipse(circle)
                Dim oldClip = g.Clip
                g.SetClip(path)
                g.DrawImage(avatar, circle)
                g.Clip = oldClip
            End Using
        Else
            Using b As New SolidBrush(Theme.AccentSoft)
                g.FillEllipse(b, circle)
            End Using
            Dim initials = If(title.Length > 0, title.Substring(0, 1).ToUpperInvariant(), "?")
            TextRenderer.DrawText(g, initials, Theme.HeadingFont, circle, Theme.Accent,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End If

        TextRenderer.DrawText(g, title, Theme.BodyBoldFont, New Rectangle(54, 10, Width - 84, 20), Theme.TextMain,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        TextRenderer.DrawText(g, subtitle, Theme.SmallFont, New Rectangle(54, 30, Width - 84, 18), Theme.TextMuted,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        TextRenderer.DrawText(g, ChrW(&HE70D), Theme.IconFontSmall, New Rectangle(Width - 28, 0, 20, Height), Theme.TextMuted,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
    End Sub

End Class


Friend Enum ToastKind
    Info
    Success
    Danger
End Enum

''' <summary>Short message at the bottom of the window that disappears by itself.</summary>
Friend Class ToastBar
    Inherits PaintedControl

    Private ReadOnly hideTimer As New Timer() With {.Interval = 4500}
    Private message As String = ""
    Private kind As ToastKind = ToastKind.Info

    Public Sub New()
        Visible = False
        BackColor = Theme.Back
        AddHandler hideTimer.Tick,
            Sub(s, e)
                hideTimer.Stop()
                Visible = False
            End Sub
    End Sub

    Public Sub ShowMessage(text As String, messageKind As ToastKind)
        message = If(text, "")
        kind = messageKind
        If Parent IsNot Nothing Then
            Dim textSize = TextRenderer.MeasureText(message, Theme.BodyFont, New System.Drawing.Size(620, 200), TextFormatFlags.WordBreak)
            Dim w = Math.Min(Parent.ClientSize.Width - 40, Math.Max(260, textSize.Width + 60))
            Dim h = Math.Max(44, textSize.Height + 24)
            SetBounds((Parent.ClientSize.Width - w) \ 2, Parent.ClientSize.Height - h - 20, w, h)
            Anchor = AnchorStyles.Bottom
        End If
        Visible = True
        BringToFront()
        Invalidate()
        hideTimer.Stop()
        hideTimer.Interval = If(kind = ToastKind.Danger, 7000, 4000)
        hideTimer.Start()
    End Sub

    Protected Overrides Sub OnClick(e As EventArgs)
        Visible = False
        MyBase.OnClick(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim accentColor = If(kind = ToastKind.Danger, Theme.Danger, If(kind = ToastKind.Success, Theme.Good, Theme.Accent))
        Theme.FillRounded(e.Graphics, New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F), 8, Theme.Blend(Theme.Surface, accentColor, 0.18), accentColor)
        Dim glyph = If(kind = ToastKind.Danger, ChrW(&HE783), If(kind = ToastKind.Success, ChrW(&HE73E), ChrW(&HE946)))
        TextRenderer.DrawText(e.Graphics, glyph, Theme.IconFont, New Rectangle(12, 0, 24, Height), accentColor,
                              TextFormatFlags.VerticalCenter Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.NoPadding)
        TextRenderer.DrawText(e.Graphics, message, Theme.BodyFont, New Rectangle(44, 10, Width - 56, Height - 20), Theme.TextMain,
                              TextFormatFlags.VerticalCenter Or TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then hideTimer.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class


''' <summary>Base class for pages: draws the page title and offers helpers.</summary>
Friend Class PageBase
    Inherits Panel

    Protected ReadOnly Main As MainForm
    Private _title As String
    Private _subtitle As String

    Protected Const Pad As Integer = 24
    Protected Const HeaderHeight As Integer = 84

    Public Sub New(owner As MainForm, title As String, subtitle As String)
        Main = owner
        _title = title
        _subtitle = subtitle
        BackColor = Theme.Back
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
        DoubleBuffered = True
        ResizeRedraw = True
    End Sub

    ''' <summary>Changes the page heading.</summary>
    Protected Sub SetHeading(title As String, subtitle As String)
        _title = If(title, "")
        _subtitle = If(subtitle, "")
        Invalidate(New Rectangle(0, 0, Width, HeaderHeight))
    End Sub

    ''' <summary>Called whenever the page becomes visible.</summary>
    Friend Overridable Sub OnShown()
    End Sub

    ''' <summary>F5: reload the page.</summary>
    Friend Overridable Sub RefreshPage()
        OnShown()
    End Sub

    ''' <summary>The active account changed (or was removed).</summary>
    Friend Overridable Sub OnAccountChanged()
    End Sub

    Protected ReadOnly Property Api As ApiClient
        Get
            Return Main.Api
        End Get
    End Property

    Protected ReadOnly Property IsConnected As Boolean
        Get
            Return Main.Api IsNot Nothing AndAlso Main.CurrentUser IsNot Nothing
        End Get
    End Property

    Private notConnectedPanel As Panel

    ''' <summary>
    ''' True if an account is connected. Otherwise a hint with "Konto hinzufügen" covers the page.
    ''' </summary>
    Protected Function RequireConnection() As Boolean
        If IsConnected Then
            If notConnectedPanel IsNot Nothing Then notConnectedPanel.Visible = False
            Return True
        End If
        If notConnectedPanel Is Nothing Then
            notConnectedPanel = New Panel() With {.BackColor = Theme.Back}
            Dim hint = MakeLabel("Du bist noch mit keinem Forum verbunden." & Environment.NewLine &
                                 "Füge ein Konto hinzu: Adresse deines Forums plus Benutzername und Passwort oder API-Key.", False)
            hint.TextAlign = ContentAlignment.MiddleCenter
            hint.AutoEllipsis = False
            Dim addButton = MakeButton("Konto hinzufügen", ButtonKind.Primary, Theme.IconAdd)
            AddHandler addButton.Click,
                Sub(sender, args)
                    Main.ShowPage(Main.AccountsView)
                    Main.AccountsView.StartAddAccount()
                End Sub
            notConnectedPanel.Controls.Add(hint)
            notConnectedPanel.Controls.Add(addButton)
            AddHandler notConnectedPanel.Resize,
                Sub(sender, args)
                    hint.SetBounds(20, notConnectedPanel.Height \ 2 - 70, notConnectedPanel.Width - 40, 60)
                    addButton.SetBounds((notConnectedPanel.Width - 190) \ 2, notConnectedPanel.Height \ 2, 190, 38)
                End Sub
            Controls.Add(notConnectedPanel)
        End If
        notConnectedPanel.SetBounds(0, HeaderHeight, Width, Math.Max(0, Height - HeaderHeight))
        notConnectedPanel.Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Right Or AnchorStyles.Bottom
        notConnectedPanel.Visible = True
        notConnectedPanel.BringToFront()
        Return False
    End Function

    ''' <summary>Disables a button while an action runs, so it cannot be clicked twice.</summary>
    Protected Async Function RunBusy(button As Control, action As Func(Of Task)) As Task
        If button IsNot Nothing Then button.Enabled = False
        Cursor = Cursors.WaitCursor
        Try
            Await action()
        Catch ex As Exception
            Main.ShowError(ex)
        Finally
            Cursor = Cursors.Default
            If button IsNot Nothing Then button.Enabled = True
        End Try
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        TextRenderer.DrawText(e.Graphics, _title, Theme.TitleFont, New Rectangle(Pad - 2, 20, Width - Pad * 2 - 360, 34), Theme.TextMain,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        TextRenderer.DrawText(e.Graphics, _subtitle, Theme.BodyFont, New Rectangle(Pad, 54, Width - Pad * 2 - 360, 22), Theme.TextMuted,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
    End Sub

    Protected Shared Function MakeLabel(text As String, Optional muted As Boolean = True, Optional small As Boolean = False) As Label
        Return New Label() With {
            .Text = text, .AutoSize = False, .AutoEllipsis = True,
            .ForeColor = If(muted, Theme.TextMuted, Theme.TextMain),
            .Font = If(small, Theme.SmallFont, Theme.BodyFont),
            .BackColor = Color.Transparent, .Height = If(small, 18, 20)}
    End Function

    Protected Shared Function MakeButton(text As String, kind As ButtonKind, Optional glyph As String = "") As ModernButton
        Return New ModernButton() With {.Text = text, .Kind = kind, .IconGlyph = glyph}
    End Function

End Class
