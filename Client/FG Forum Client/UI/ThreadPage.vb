''' <summary>Shows one thread with all posts, quick reply and moderation buttons.</summary>
Friend Class ThreadPage
    Inherits PageBase

    Private ReadOnly backButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconBack)
    Private ReadOnly refreshButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconRefresh)
    Private ReadOnly closeButton As ModernButton = MakeButton("Schließen", ButtonKind.Secondary, Theme.IconLock)
    Private ReadOnly stickButton As ModernButton = MakeButton("Anpinnen", ButtonKind.Secondary, Theme.IconPin)
    Private ReadOnly replyButton As ModernButton = MakeButton("Antworten", ButtonKind.Primary, Theme.IconReply)

    Private ReadOnly view As New HtmlView()
    Private ReadOnly pager As New Panel() With {.BackColor = Theme.Back}
    Private ReadOnly firstButton As ModernButton = MakeButton("", ButtonKind.Secondary, ChrW(&HE892))
    Private ReadOnly previousButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconBack)
    Private ReadOnly pageLabel As Label = MakeLabel("", True, True)
    Private ReadOnly nextButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconForward)
    Private ReadOnly lastButton As ModernButton = MakeButton("", ButtonKind.Secondary, ChrW(&HE893))

    Private ReadOnly replyPanel As New Panel() With {.BackColor = Theme.Back}
    Private ReadOnly quickEditor As New MyCodeEditor()
    Private ReadOnly sendButton As ModernButton = MakeButton("Senden", ButtonKind.Primary, Theme.IconSend)
    Private ReadOnly advancedButton As ModernButton = MakeButton("Erweitert", ButtonKind.Secondary, Theme.IconEdit)
    Private ReadOnly attachInfo As Label = MakeLabel("", True, True)
    Private ReadOnly quickAttachments As AttachmentPanel
    Private ReadOnly draftTimer As New Timer() With {.Interval = 1500}

    Private data As ThreadPageData
    Private threadId As Integer
    Private page As Integer = 1
    Private scrollToPid As Integer
    Private scrollToEnd As Boolean
    Private quickHash As String = ApiClient.NewPostHash()
    Private loadVersion As Integer

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Thema", "")
        quickAttachments = New AttachmentPanel(owner) With {.Visible = False}
        pageLabel.TextAlign = ContentAlignment.MiddleCenter
        attachInfo.TextAlign = ContentAlignment.MiddleLeft
        quickEditor.Box.Font = Theme.BodyFont

        pager.Controls.AddRange(New Control() {firstButton, previousButton, pageLabel, nextButton, lastButton})
        replyPanel.Controls.AddRange(New Control() {quickEditor, sendButton, advancedButton, attachInfo, quickAttachments})
        Controls.AddRange(New Control() {backButton, refreshButton, closeButton, stickButton, replyButton, view, pager, replyPanel})

        Dim tips As New ToolTip()
        tips.SetToolTip(backButton, "Zurück (Alt+Links)")
        tips.SetToolTip(refreshButton, "Neu laden (F5)")
        tips.SetToolTip(firstButton, "Erste Seite")
        tips.SetToolTip(lastButton, "Letzte Seite")

        AddHandler backButton.Click, Sub(s, e) Main.GoBack()
        AddHandler refreshButton.Click, Sub(s, e) RefreshPage()
        AddHandler replyButton.Click, Sub(s, e) OpenAdvancedEditor()
        AddHandler advancedButton.Click, Sub(s, e) OpenAdvancedEditor()
        AddHandler sendButton.Click, AddressOf Send_Click
        AddHandler closeButton.Click, AddressOf Close_Click
        AddHandler stickButton.Click, AddressOf Stick_Click
        AddHandler firstButton.Click, Sub(s, e) GoToPage(1)
        AddHandler previousButton.Click, Sub(s, e) GoToPage(page - 1)
        AddHandler nextButton.Click, Sub(s, e) GoToPage(page + 1)
        AddHandler lastButton.Click, Sub(s, e) GoToPage(If(data Is Nothing, 1, data.Pages))
        AddHandler view.ActionClicked, AddressOf View_ActionClicked
        AddHandler view.PageLoaded, AddressOf View_PageLoaded
        AddHandler quickEditor.Box.FilesAdded, Sub(s, e) AddQuickFiles(e.Files)
        AddHandler quickEditor.AttachClicked, AddressOf ChooseFiles
        AddHandler quickAttachments.InsertRequested, Sub(s, e) quickEditor.Box.InsertText(e.Files(0) & Environment.NewLine)
        AddHandler quickEditor.Box.TextChanged,
            Sub(s, e)
                draftTimer.Stop()
                draftTimer.Start()
            End Sub
        AddHandler quickEditor.Box.KeyDown,
            Sub(s, e)
                If e.Control AndAlso e.KeyCode = Keys.Enter Then
                    e.SuppressKeyPress = True
                    Send_Click(sendButton, EventArgs.Empty)
                End If
            End Sub
        AddHandler draftTimer.Tick,
            Sub(s, e)
                draftTimer.Stop()
                SaveQuickDraft()
            End Sub

        view.ShowHtml(HtmlBuilder.Notice("Lade …"))
    End Sub

    ' ---------- Loading ----------

    Friend Sub LoadThread(tid As Integer, Optional pid As Integer = 0, Optional lastPage As Boolean = False)
        If tid <> threadId Then
            SaveQuickDraft()
            threadId = tid
            quickHash = ApiClient.NewPostHash()
            quickAttachments.Visible = False
            attachInfo.Text = ""
            data = Nothing
            SetHeading("Lade Thema …", "")
            view.ShowHtml(HtmlBuilder.Notice("Lade …"))
            Dim draft = Drafts.Load(QuickDraftKey())
            quickEditor.Text = If(draft Is Nothing, "", draft.Item2)
        End If
        scrollToPid = pid
        scrollToEnd = lastPage AndAlso pid = 0
        page = If(lastPage, -1, 1)
        LoadPageAsync(pid)
    End Sub

    Friend Overrides Sub OnShown()
        RequireConnection()
    End Sub

    Friend Overrides Sub RefreshPage()
        If threadId > 0 Then LoadPageAsync(0)
    End Sub

    Friend Overrides Sub OnAccountChanged()
        threadId = 0
        data = Nothing
        view.ShowHtml(HtmlBuilder.Notice(""))
    End Sub

    Private Sub GoToPage(target As Integer)
        If data Is Nothing Then Return
        target = Math.Max(1, Math.Min(data.Pages, target))
        If target = data.Page Then Return
        page = target
        scrollToPid = 0
        scrollToEnd = False
        LoadPageAsync(0)
    End Sub

    Private Async Sub LoadPageAsync(pid As Integer)
        If threadId <= 0 OrElse Not IsConnected Then Return
        loadVersion += 1
        Dim version = loadVersion
        Try
            Dim query As New Dictionary(Of String, Object) From {{"tid", threadId}, {"perpage", Main.Store.Settings.PostsPerPage}}
            If pid > 0 Then
                query("pid") = pid
            ElseIf page = -1 Then
                query("page") = "last"
            Else
                query("page") = page
            End If
            Dim result = ThreadPageData.From(Await Api.GetAsync("thread", query))
            If version <> loadVersion Then Return

            data = result
            page = result.Page
            SetHeading(result.Thread.DisplaySubject,
                       result.ForumName & " · " & Fmt.Number(result.Thread.Replies) & " Antworten · " & Fmt.Number(result.Thread.Views) & " Aufrufe" &
                       If(result.Thread.Closed, " · geschlossen", ""))
            UpdateControls()
            view.ShowHtml(HtmlBuilder.Thread(result, Main.Store.Settings.ShowSignatures))
        Catch ex As ApiException When ex.Code = "thread_not_found"
            view.ShowHtml(HtmlBuilder.Notice("Dieses Thema gibt es nicht (mehr) oder du darfst es nicht sehen."))
            SetHeading("Thema nicht gefunden", "")
        Catch ex As Exception
            Main.ShowError(ex)
        End Try
    End Sub

    Private Sub UpdateControls()
        If data Is Nothing Then Return
        pageLabel.Text = "Seite " & data.Page & " von " & data.Pages & "  ·  " & data.Total & " Beiträge"
        firstButton.Enabled = data.Page > 1
        previousButton.Enabled = data.Page > 1
        nextButton.Enabled = data.Page < data.Pages
        lastButton.Enabled = data.Page < data.Pages
        replyButton.Visible = data.CanReply
        replyPanel.Visible = data.CanReply
        closeButton.Visible = data.CanClose
        closeButton.Text = If(data.Thread.Closed, "Öffnen", "Schließen")
        closeButton.IconGlyph = If(data.Thread.Closed, Theme.IconUnlock, Theme.IconLock)
        stickButton.Visible = data.CanStick
        stickButton.Text = If(data.Thread.Sticky, "Lösen", "Anpinnen")
        quickEditor.Box.Enabled = data.CanReply
        PerformLayout()
    End Sub

    Private Async Sub View_PageLoaded(sender As Object, e As EventArgs)
        If data IsNot Nothing Then
            If scrollToPid > 0 OrElse data.FocusPid > 0 Then
                view.ScrollTo("pid" & If(scrollToPid > 0, scrollToPid, data.FocusPid))
            ElseIf scrollToEnd Then
                view.ScrollToBottom()
            End If
        End If
        Try
            Await Main.Images.FillImagesAsync(view, Api)
        Catch
        End Try
    End Sub

    ' ---------- Actions inside the posts ----------

    Private Async Sub View_ActionClicked(sender As Object, e As HtmlActionEventArgs)
        If data Is Nothing Then Return
        Dim post = data.Posts.FirstOrDefault(Function(p) p.Pid = e.Id)
        Select Case e.Action
            Case "quote"
                If post Is Nothing Then Return
                quickEditor.Box.SelectionStart = quickEditor.Box.TextLength
                quickEditor.Box.InsertText(post.QuoteMyCode() & Environment.NewLine)
                quickEditor.Box.Focus()
            Case "edit"
                Main.OpenEdit(e.Id)
            Case "delete"
                If post Is Nothing Then Return
                Await DeletePostAsync(post)
            Case "copylink"
                Try
                    Clipboard.SetText(Main.Api.ForumUrl & "/showthread.php?pid=" & e.Id & "#pid" & e.Id)
                    Main.ShowToast("Link zum Beitrag kopiert.", ToastKind.Success)
                Catch
                End Try
            Case "user"
                Main.OpenUser(e.Id)
            Case "image"
                Await OpenAttachmentAsync(e.Id, True)
            Case "download"
                Await OpenAttachmentAsync(e.Id, False)
        End Select
    End Sub

    Private Async Function DeletePostAsync(post As PostInfo) As Task
        Dim question = If(post.IsFirst,
                          "Das ist der erste Beitrag. Dadurch wird das ganze Thema """ & data.Thread.Subject & """ gelöscht." & Environment.NewLine & "Wirklich löschen?",
                          "Diesen Beitrag von " & post.Author.Username & " wirklich löschen?")
        If MessageBox.Show(Main, question, "Löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
        Try
            Dim result = Await Api.PostAsync("post/delete", New Dictionary(Of String, Object) From {{"pid", post.Pid}})
            If Json.GetBool(result, "thread_deleted") Then
                Main.ShowToast("Das Thema wurde gelöscht.", ToastKind.Success)
                Main.OpenForum(Json.GetInt(result, "fid"))
            Else
                Main.ShowToast("Der Beitrag wurde gelöscht.", ToastKind.Success)
                LoadPageAsync(0)
            End If
        Catch ex As Exception
            Main.ShowError(ex)
        End Try
    End Function

    ''' <summary>Images open in the Windows viewer, other files can be saved.</summary>
    Private Async Function OpenAttachmentAsync(aid As Integer, isImage As Boolean) As Task
        Dim attachment = data.Posts.SelectMany(Function(p) p.Attachments).FirstOrDefault(Function(a) a.Aid = aid)
        Dim fileName = If(attachment Is Nothing, "Anhang_" & aid, attachment.FileName)
        Try
            If isImage Then
                Main.ShowToast("Lade " & fileName & " …")
                Dim bytes = Await Main.Images.GetImageAsync(Api, aid, False)
                Dim folder = Path.Combine(Path.GetTempPath(), "FG Forum Client")
                Directory.CreateDirectory(folder)
                Dim target = Path.Combine(folder, aid & "_" & SafeFileName(fileName))
                File.WriteAllBytes(target, bytes)
                Process.Start(New ProcessStartInfo(target) With {.UseShellExecute = True})
            Else
                Using dialog As New SaveFileDialog() With {.FileName = SafeFileName(fileName), .Title = "Anhang speichern"}
                    If dialog.ShowDialog(Main) <> DialogResult.OK Then Return
                    Main.ShowToast("Lade " & fileName & " …")
                    Dim fileBytes = Await Api.DownloadAsync("attachment", New Dictionary(Of String, Object) From {{"aid", aid}})
                    File.WriteAllBytes(dialog.FileName, fileBytes)
                    Main.ShowToast(fileName & " gespeichert.", ToastKind.Success)
                End Using
            End If
        Catch ex As Exception
            Main.ShowError(ex)
        End Try
    End Function

    Private Shared Function SafeFileName(name As String) As String
        Dim result = name
        For Each c As Char In Path.GetInvalidFileNameChars()
            result = result.Replace(c, "_"c)
        Next
        Return result
    End Function

    ' ---------- Moderation ----------

    Private Async Sub Close_Click(sender As Object, e As EventArgs)
        If data Is Nothing Then Return
        Await Moderate(If(data.Thread.Closed, "open", "close"), closeButton)
    End Sub

    Private Async Sub Stick_Click(sender As Object, e As EventArgs)
        If data Is Nothing Then Return
        Await Moderate(If(data.Thread.Sticky, "unstick", "stick"), stickButton)
    End Sub

    Private Async Function Moderate(action As String, button As Control) As Task
        Await RunBusy(button,
            Async Function()
                Await Api.PostAsync("thread/moderate", New Dictionary(Of String, Object) From {{"tid", threadId}, {"action", action}})
                Dim texts As New Dictionary(Of String, String) From {
                    {"close", "Thema geschlossen."}, {"open", "Thema wieder geöffnet."},
                    {"stick", "Thema angepinnt."}, {"unstick", "Thema gelöst."}}
                Main.ShowToast(texts(action), ToastKind.Success)
                LoadPageAsync(0)
            End Function)
    End Function

    ' ---------- Quick reply ----------

    Private Function QuickDraftKey() As String
        Dim account = Main.Store.Active
        Return "quick_" & If(account Is Nothing, "x", account.Id) & "_" & threadId
    End Function

    Private Sub SaveQuickDraft()
        If threadId > 0 Then Drafts.Save(QuickDraftKey(), "", quickEditor.Text)
    End Sub

    Private Sub ChooseFiles(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog() With {.Multiselect = True, .Title = "Dateien anhängen"}
            If dialog.ShowDialog(Main) = DialogResult.OK Then AddQuickFiles(dialog.FileNames.ToList())
        End Using
    End Sub

    Private Sub AddQuickFiles(files As List(Of String))
        If data Is Nothing OrElse Not data.CanAttach Then
            Main.ShowToast("In diesem Forum darfst du keine Dateien anhängen.", ToastKind.Danger)
            Return
        End If
        If Not quickAttachments.Visible Then
            quickAttachments.ConfigureNew(0, threadId, quickHash)
            quickAttachments.Visible = True
            PerformLayout()
        End If
        quickAttachments.AddFiles(files)
        attachInfo.Text = "Anhänge werden mit der Antwort gesendet."
    End Sub

    Private Async Sub Send_Click(sender As Object, e As EventArgs)
        If data Is Nothing OrElse Not data.CanReply Then Return
        Dim message = quickEditor.Text.Trim()
        If message.Length = 0 Then
            Main.ShowToast("Schreib zuerst etwas.", ToastKind.Info)
            quickEditor.Box.Focus()
            Return
        End If
        If quickAttachments.IsUploading Then
            Main.ShowToast("Bitte warte, bis alle Dateien hochgeladen sind.", ToastKind.Info)
            Return
        End If
        Await RunBusy(sendButton,
            Async Function()
                Dim result = Await Api.PostAsync("post/reply", New Dictionary(Of String, Object) From {
                    {"tid", threadId}, {"message", message}, {"posthash", quickHash}})
                Drafts.Delete(QuickDraftKey())
                quickEditor.Text = ""
                quickHash = ApiClient.NewPostHash()
                quickAttachments.Visible = False
                attachInfo.Text = ""
                PerformLayout()
                Main.ShowToast(Json.GetString(result, "message", "Antwort gesendet."), ToastKind.Success)
                Dim newPid = Json.GetInt(result, "pid")
                scrollToPid = newPid
                LoadPageAsync(newPid)
            End Function)
    End Sub

    Private Sub OpenAdvancedEditor()
        If data Is Nothing Then Return
        Dim draftText = quickEditor.Text
        quickEditor.Text = ""
        Drafts.Delete(QuickDraftKey())
        Main.OpenReply(data.Thread, draftText)
    End Sub

    ' ---------- Layout ----------

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If replyPanel Is Nothing OrElse lastButton Is Nothing Then Return

        Dim rightEdge = ClientSize.Width - Pad
        replyButton.SetBounds(rightEdge - 130, 30, 130, 36)
        Dim x = replyButton.Left - 8
        If stickButton.Visible Then
            stickButton.SetBounds(x - 122, 30, 122, 36)
            x = stickButton.Left - 8
        End If
        If closeButton.Visible Then
            closeButton.SetBounds(x - 122, 30, 122, 36)
            x = closeButton.Left - 8
        End If
        refreshButton.SetBounds(x - 40, 30, 40, 36)
        backButton.SetBounds(refreshButton.Left - 48, 30, 40, 36)

        Dim areaWidth = ClientSize.Width - Pad * 2
        Dim replyHeight = If(replyPanel.Visible, If(quickAttachments.Visible, 260, 180), 0)
        Dim pagerHeight = 44
        Dim top = HeaderHeight - 6
        Dim viewHeight = Math.Max(120, ClientSize.Height - top - pagerHeight - replyHeight - 12)
        view.SetBounds(Pad - 14, top, areaWidth + 28, viewHeight)
        pager.SetBounds(Pad, view.Bottom + 4, areaWidth, pagerHeight - 4)
        firstButton.SetBounds(0, 4, 36, 32)
        previousButton.SetBounds(42, 4, 36, 32)
        lastButton.SetBounds(areaWidth - 36, 4, 36, 32)
        nextButton.SetBounds(areaWidth - 78, 4, 36, 32)
        pageLabel.SetBounds(84, 4, areaWidth - 168, 32)

        If replyPanel.Visible Then
            replyPanel.SetBounds(Pad, pager.Bottom + 4, areaWidth, replyHeight - 8)
            Dim buttonsWidth = 130
            Dim editorHeight = If(quickAttachments.Visible, 150, replyHeight - 12)
            quickEditor.SetBounds(0, 0, areaWidth - buttonsWidth - 12, editorHeight)
            sendButton.SetBounds(areaWidth - buttonsWidth, 0, buttonsWidth, 38)
            advancedButton.SetBounds(areaWidth - buttonsWidth, 46, buttonsWidth, 34)
            attachInfo.SetBounds(areaWidth - buttonsWidth, 88, buttonsWidth, 50)
            If quickAttachments.Visible Then
                quickAttachments.SetBounds(0, editorHeight + 8, areaWidth - buttonsWidth - 12, replyHeight - editorHeight - 20)
            End If
        End If
    End Sub

End Class
