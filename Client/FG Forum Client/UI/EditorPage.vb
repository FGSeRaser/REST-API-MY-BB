Friend Enum EditorMode
    NewThread
    Reply
    Edit
    Message
End Enum

''' <summary>Full editor: new threads, replies, editing posts and private messages, with live preview and uploads.</summary>
Friend Class EditorPage
    Inherits PageBase

    Private ReadOnly cancelButton As ModernButton = MakeButton("Abbrechen", ButtonKind.Secondary)
    Private ReadOnly templateButton As ModernButton = MakeButton("Vorlagen", ButtonKind.Secondary, Theme.IconRocket)
    Private ReadOnly submitButton As ModernButton = MakeButton("Absenden", ButtonKind.Primary, Theme.IconSend)

    Private ReadOnly topLabel As Label = MakeLabel("", True, True)
    Private ReadOnly forumCombo As New DarkComboBox()
    Private ReadOnly recipientBox As New DarkTextBox() With {.Placeholder = "Empfänger (mehrere mit Komma trennen)"}
    Private ReadOnly contextLabel As Label = MakeLabel("", False)
    Private ReadOnly subjectLabel As Label = MakeLabel("Betreff", True, True)
    Private ReadOnly subjectBox As New DarkTextBox() With {.Placeholder = "Worum geht es?"}
    Private ReadOnly reasonBox As New DarkTextBox() With {.Placeholder = "Grund der Änderung (optional)"}
    Private ReadOnly editor As New MyCodeEditor()
    Private ReadOnly attachmentsLabel As Label = MakeLabel("Anhänge", True, True)
    Private ReadOnly attachments As AttachmentPanel
    Private ReadOnly signatureToggle As New ToggleSwitch() With {.Text = "Signatur anhängen", .Checked = True}
    Private ReadOnly closeToggle As New ToggleSwitch() With {.Text = "Thema schließen"}
    Private ReadOnly stickToggle As New ToggleSwitch() With {.Text = "Thema anpinnen"}

    Private ReadOnly previewCard As New CardPanel() With {.Title = "Vorschau", .Subtitle = "So sieht der Beitrag im Forum aus"}
    Private ReadOnly previewToggle As New ToggleSwitch() With {.Text = "Live"}
    Private ReadOnly preview As New HtmlView()
    Private ReadOnly previewTimer As New Timer() With {.Interval = 900}
    Private ReadOnly draftTimer As New Timer() With {.Interval = 2000}

    Private mode As EditorMode = EditorMode.NewThread
    Private forumId As Integer
    Private threadId As Integer
    Private postId As Integer
    Private replyToPm As Integer
    Private threadSubject As String = ""
    Private posthash As String = ""
    Private draftKey As String = ""
    Private loadingContent As Boolean
    Private previewVersion As Integer
    Private openReleaseAssistant As Boolean

    Public Property OriginPage As PageBase

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Editor", "")
        attachments = New AttachmentPanel(owner)
        previewCard.Controls.AddRange(New Control() {previewToggle, preview})
        Controls.AddRange(New Control() {cancelButton, templateButton, submitButton, topLabel, forumCombo, recipientBox, contextLabel,
                                         subjectLabel, subjectBox, reasonBox, editor, attachmentsLabel, attachments,
                                         signatureToggle, closeToggle, stickToggle, previewCard})

        AddHandler cancelButton.Click, Sub(s, e) Cancel()
        AddHandler submitButton.Click, AddressOf Submit_Click
        AddHandler templateButton.Click, AddressOf Templates_Click
        AddHandler editor.Box.FilesAdded, Sub(s, e) AddFiles(e.Files)
        AddHandler editor.AttachClicked, AddressOf ChooseFiles
        AddHandler attachments.InsertRequested, Sub(s, e) editor.Box.InsertText(e.Files(0) & Environment.NewLine)
        AddHandler editor.Box.TextChanged, AddressOf Content_Changed
        AddHandler subjectBox.TextChanged, AddressOf Content_Changed
        AddHandler previewToggle.CheckedChanged,
            Sub(s, e)
                Main.Store.Settings.LivePreview = previewToggle.Checked
                Main.Store.Save()
                If previewToggle.Checked Then UpdatePreviewAsync()
            End Sub
        AddHandler previewTimer.Tick,
            Sub(s, e)
                previewTimer.Stop()
                UpdatePreviewAsync()
            End Sub
        AddHandler draftTimer.Tick,
            Sub(s, e)
                draftTimer.Stop()
                SaveDraftNow()
            End Sub
        AddHandler preview.PageLoaded,
            Async Sub(s, e)
                Try
                    Await Main.Images.FillImagesAsync(preview, Api)
                Catch
                End Try
            End Sub
        AddHandler editor.Box.KeyDown,
            Sub(s, e)
                If e.Control AndAlso e.KeyCode = Keys.Enter Then
                    e.SuppressKeyPress = True
                    Submit_Click(submitButton, EventArgs.Empty)
                End If
            End Sub
        preview.ShowHtml(HtmlBuilder.Preview(""))
    End Sub

    Friend Overrides Sub OnShown()
        If Not RequireConnection() Then Return
        previewToggle.Checked = Main.Store.Settings.LivePreview
        editor.Box.Focus()
        If openReleaseAssistant Then
            openReleaseAssistant = False
            BeginInvoke(New Action(AddressOf ShowReleaseAssistant))
        End If
    End Sub

    Friend Overrides Sub RefreshPage()
        UpdatePreviewAsync()
    End Sub

    ' ---------- Start the different modes ----------

    Private Sub Reset(newMode As EditorMode, origin As PageBase)
        SaveDraftNow()
        loadingContent = True
        mode = newMode
        OriginPage = If(origin Is Me, OriginPage, origin)
        forumId = 0
        threadId = 0
        postId = 0
        replyToPm = 0
        threadSubject = ""
        posthash = ApiClient.NewPostHash()
        subjectBox.Text = ""
        reasonBox.Text = ""
        recipientBox.Text = ""
        editor.Text = ""
        closeToggle.Checked = False
        stickToggle.Checked = False
        signatureToggle.Checked = True
        preview.ShowHtml(HtmlBuilder.Preview(""))
        loadingContent = False
    End Sub

    Friend Sub StartNewThread(fid As Integer, origin As PageBase)
        Reset(EditorMode.NewThread, origin)
        forumId = fid
        SetHeading("Neues Thema", "Schreibe ein neues Thema – Bilder einfach hineinziehen oder mit Strg+V einfügen")
        submitButton.Text = "Thema erstellen"
        attachments.ConfigureNew(fid, 0, posthash)
        LoadForumsAsync(fid)
        UpdateModeControls()
    End Sub

    ''' <summary>"Tool veröffentlichen" on the start page: new thread plus the release assistant.</summary>
    Friend Sub StartReleaseFromHome(origin As PageBase)
        StartNewThread(0, origin)
        openReleaseAssistant = True
        Main.ShowPage(Me, True)
    End Sub

    Friend Sub StartReply(thread As ThreadInfo, initialText As String, origin As PageBase)
        Reset(EditorMode.Reply, origin)
        threadId = thread.Tid
        forumId = thread.Fid
        threadSubject = thread.Subject
        SetHeading("Antworten", thread.DisplaySubject)
        contextLabel.Text = "Antwort auf: " & thread.DisplaySubject
        submitButton.Text = "Antwort senden"
        attachments.ConfigureNew(thread.Fid, thread.Tid, posthash)
        draftKey = "reply_" & AccountKey() & "_" & thread.Tid
        RestoreDraft(initialText)
        UpdateModeControls()
    End Sub

    Friend Async Sub StartEdit(pid As Integer, origin As PageBase)
        Reset(EditorMode.Edit, origin)
        postId = pid
        SetHeading("Beitrag bearbeiten", "Lade Beitrag …")
        submitButton.Text = "Speichern"
        submitButton.Enabled = False
        UpdateModeControls()
        Try
            Dim data = Await Api.GetAsync("post/get", New Dictionary(Of String, Object) From {{"pid", pid}})
            Dim post = PostInfo.From(data)
            Dim thread = ThreadInfo.From(Json.GetObject(data, "thread"))
            threadId = post.Tid
            forumId = thread.Fid
            threadSubject = thread.Subject
            loadingContent = True
            subjectBox.Text = post.Subject
            editor.Text = post.Message
            loadingContent = False
            attachments.ConfigureEdit(pid, post.Attachments)
            SetHeading("Beitrag bearbeiten", thread.DisplaySubject)
            contextLabel.Text = "Beitrag #" & pid & " von " & post.Author.Username & " in """ & thread.Subject & """"
            draftKey = "edit_" & AccountKey() & "_" & pid
            submitButton.Enabled = post.CanEdit
            If Not post.CanEdit Then Main.ShowToast("Du darfst diesen Beitrag nicht (mehr) bearbeiten.", ToastKind.Danger)
            UpdatePreviewAsync()
        Catch ex As Exception
            Main.ShowError(ex)
        End Try
    End Sub

    Friend Sub StartMessage(recipient As String, subject As String, initialText As String, replyTo As Integer, origin As PageBase)
        Reset(EditorMode.Message, origin)
        replyToPm = replyTo
        SetHeading(If(replyTo > 0, "Antwort schreiben", "Neue Nachricht"), "Private Nachricht")
        submitButton.Text = "Nachricht senden"
        loadingContent = True
        recipientBox.Text = recipient
        subjectBox.Text = subject
        loadingContent = False
        draftKey = "pm_" & AccountKey() & "_" & If(replyTo > 0, replyTo.ToString(), "neu")
        RestoreDraft(initialText)
        UpdateModeControls()
    End Sub

    Private Function AccountKey() As String
        Dim account = Main.Store.Active
        Return If(account Is Nothing, "x", account.Id)
    End Function

    Private Sub RestoreDraft(initialText As String)
        loadingContent = True
        Dim draft = Drafts.Load(draftKey)
        If draft IsNot Nothing AndAlso draft.Item2.Trim().Length > 0 Then
            If draft.Item1.Length > 0 AndAlso subjectBox.Text.Length = 0 Then subjectBox.Text = draft.Item1
            editor.Text = draft.Item2 & If(String.IsNullOrEmpty(initialText), "", Environment.NewLine & initialText)
            Main.ShowToast("Dein gespeicherter Entwurf wurde wiederhergestellt.", ToastKind.Info)
        Else
            editor.Text = If(initialText, "")
        End If
        editor.Box.SelectionStart = editor.Box.TextLength
        loadingContent = False
        UpdatePreviewAsync()
    End Sub

    Private Sub UpdateModeControls()
        Dim isThread = mode = EditorMode.NewThread
        Dim isPm = mode = EditorMode.Message
        forumCombo.Visible = isThread
        topLabel.Visible = isThread OrElse isPm
        topLabel.Text = If(isPm, "Empfänger", "Forum")
        recipientBox.Visible = isPm
        contextLabel.Visible = mode = EditorMode.Reply OrElse mode = EditorMode.Edit
        subjectLabel.Text = If(mode = EditorMode.Reply, "Betreff (optional)", "Betreff")
        reasonBox.Visible = mode = EditorMode.Edit
        attachments.Visible = Not isPm
        attachmentsLabel.Visible = Not isPm
        signatureToggle.Visible = mode <> EditorMode.Edit
        Dim canModerate = Main.CurrentUser IsNot Nothing AndAlso Main.CurrentUser.IsModerator AndAlso (isThread OrElse mode = EditorMode.Reply)
        closeToggle.Visible = canModerate
        stickToggle.Visible = canModerate
        templateButton.Visible = Not isPm
        submitButton.Enabled = mode <> EditorMode.Edit OrElse postId > 0
        PerformLayout()
    End Sub

    Private Async Sub LoadForumsAsync(selectFid As Integer)
        Try
            Dim forums = Await Main.ForumsView.GetForumsAsync()
            forumCombo.Items.Clear()
            For Each forum As ForumInfo In forums
                If forum.Type = "f" AndAlso forum.CanPostThreads AndAlso forum.LinkTo.Length = 0 Then forumCombo.Items.Add(forum)
            Next
            Dim index = -1
            For i = 0 To forumCombo.Items.Count - 1
                If DirectCast(forumCombo.Items(i), ForumInfo).Fid = selectFid Then index = i
            Next
            If index < 0 AndAlso forumCombo.Items.Count > 0 Then index = 0
            forumCombo.SelectedIndex = index
            If forumCombo.Items.Count = 0 Then Main.ShowToast("Du darfst in keinem Forum Themen erstellen.", ToastKind.Danger)
            Dim chosen = SelectedForum()
            If mode = EditorMode.NewThread AndAlso chosen IsNot Nothing AndAlso editor.Text.Length = 0 Then
                draftKey = "thread_" & AccountKey() & "_" & chosen.Fid
                RestoreDraft("")
            End If
        Catch ex As Exception
            Main.ShowError(ex)
        End Try
    End Sub

    Private Function SelectedForum() As ForumInfo
        Return TryCast(forumCombo.SelectedItem, ForumInfo)
    End Function

    ' ---------- Drafts and preview ----------

    Private Sub Content_Changed(sender As Object, e As EventArgs)
        If loadingContent Then Return
        draftTimer.Stop()
        draftTimer.Start()
        If previewToggle.Checked Then
            previewTimer.Stop()
            previewTimer.Start()
        End If
    End Sub

    Friend Sub SaveDraftNow()
        If loadingContent Then Return
        Dim key = draftKey
        If mode = EditorMode.NewThread Then
            Dim forum = SelectedForum()
            key = "thread_" & AccountKey() & "_" & If(forum Is Nothing, 0, forum.Fid)
        End If
        If key.Length = 0 Then Return
        Drafts.Save(key, subjectBox.Text, editor.Text)
    End Sub

    Private Async Sub UpdatePreviewAsync()
        If Not IsConnected Then Return
        previewVersion += 1
        Dim version = previewVersion
        Dim message = editor.Text
        If message.Trim().Length = 0 Then
            preview.ShowHtml(HtmlBuilder.Preview(""))
            Return
        End If
        Try
            Dim body As New Dictionary(Of String, Object) From {{"message", message}}
            If mode = EditorMode.Message Then
                body("pm") = True
            ElseIf mode = EditorMode.NewThread Then
                Dim forum = SelectedForum()
                If forum Is Nothing Then Return
                body("fid") = forum.Fid
            Else
                body("tid") = threadId
            End If
            Dim result = Await Api.PostAsync("preview", body)
            If version <> previewVersion Then Return
            preview.ShowHtml(HtmlBuilder.Preview(Json.GetString(result, "html")))
        Catch ex As ApiException
            If version = previewVersion Then preview.ShowHtml(HtmlBuilder.Notice("Vorschau nicht möglich: " & ex.Message))
        Catch
        End Try
    End Sub

    ' ---------- Attachments ----------

    Private Sub ChooseFiles(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog() With {.Multiselect = True, .Title = "Dateien anhängen"}
            If dialog.ShowDialog(Main) = DialogResult.OK Then AddFiles(dialog.FileNames.ToList())
        End Using
    End Sub

    Private Sub AddFiles(files As List(Of String))
        If mode = EditorMode.Message Then
            Main.ShowToast("Private Nachrichten können in MyBB keine Anhänge haben.", ToastKind.Info)
            Return
        End If
        If mode = EditorMode.NewThread Then
            Dim forum = SelectedForum()
            If forum Is Nothing Then
                Main.ShowToast("Wähle zuerst ein Forum.", ToastKind.Info)
                Return
            End If
            If Not forum.CanAttach Then
                Main.ShowToast("In diesem Forum darfst du keine Dateien anhängen.", ToastKind.Danger)
                Return
            End If
            attachments.ConfigureNewKeep(forum.Fid)
        End If
        attachments.AddFiles(files)
    End Sub

    ' ---------- Templates ----------

    Private Sub Templates_Click(sender As Object, e As EventArgs)
        Dim menu As New ContextMenuStrip()
        menu.Items.Add("Tool veröffentlichen (Assistent) …", Nothing, Sub(s, args) ShowReleaseAssistant())
        menu.Items.Add(New ToolStripSeparator())
        menu.Items.Add("Fehlerbericht", Nothing, Sub(s, args) InsertTemplate(
            "[b]Was passiert?[/b]" & Environment.NewLine & Environment.NewLine &
            "[b]Was hätte passieren sollen?[/b]" & Environment.NewLine & Environment.NewLine &
            "[b]So kann man es nachstellen[/b]" & Environment.NewLine & "[list=1]" & Environment.NewLine & "[*]" & Environment.NewLine & "[/list]" & Environment.NewLine & Environment.NewLine &
            "[b]Version / System[/b]" & Environment.NewLine))
        menu.Items.Add("Changelog", Nothing, Sub(s, args) InsertTemplate(
            "[b]Version X.Y – " & DateTime.Now.ToString("dd.MM.yyyy") & "[/b]" & Environment.NewLine &
            "[list]" & Environment.NewLine & "[*]Neu: " & Environment.NewLine & "[*]Verbessert: " & Environment.NewLine & "[*]Behoben: " & Environment.NewLine & "[/list]" & Environment.NewLine))
        menu.Items.Add("Anleitung", Nothing, Sub(s, args) InsertTemplate(
            "[size=large][b]Anleitung: [/b][/size]" & Environment.NewLine & Environment.NewLine &
            "[b]Du brauchst[/b]" & Environment.NewLine & "[list]" & Environment.NewLine & "[*]" & Environment.NewLine & "[/list]" & Environment.NewLine & Environment.NewLine &
            "[b]Schritt 1[/b]" & Environment.NewLine & Environment.NewLine & "[b]Schritt 2[/b]" & Environment.NewLine))
        menu.Show(templateButton, New Point(0, templateButton.Height))
    End Sub

    Private Sub InsertTemplate(template As String)
        editor.Box.InsertText(template)
    End Sub

    Private Sub ShowReleaseAssistant()
        Dim username = If(Main.CurrentUser Is Nothing, AppInfo.Author, Main.CurrentUser.User.Username)
        Using dialog As New ReleaseDialog(username)
            If dialog.ShowDialog(Main) <> DialogResult.OK Then Return
            If mode = EditorMode.NewThread OrElse subjectBox.Text.Trim().Length = 0 Then subjectBox.Text = dialog.ResultSubject
            If editor.Text.Trim().Length > 0 Then
                editor.Box.InsertText(Environment.NewLine & dialog.ResultMessage)
            Else
                editor.Text = dialog.ResultMessage
            End If
            If dialog.Files.Count > 0 Then AddFiles(dialog.Files)
        End Using
    End Sub

    ' ---------- Submit ----------

    Private Sub Cancel()
        If editor.Text.Trim().Length > 0 Then
            SaveDraftNow()
            Main.ShowToast("Dein Text wurde als Entwurf gespeichert.", ToastKind.Info)
        End If
        Main.GoBack()
    End Sub

    Private Async Sub Submit_Click(sender As Object, e As EventArgs)
        If Not submitButton.Enabled Then Return
        If attachments.IsUploading Then
            Main.ShowToast("Bitte warte, bis alle Dateien hochgeladen sind.", ToastKind.Info)
            Return
        End If
        Dim message = editor.Text
        If message.Trim().Length = 0 Then
            Main.ShowToast("Der Text ist noch leer.", ToastKind.Info)
            editor.Box.Focus()
            Return
        End If

        Await RunBusy(submitButton,
            Async Function()
                Select Case mode
                    Case EditorMode.NewThread
                        Await SubmitThread(message)
                    Case EditorMode.Reply
                        Await SubmitReply(message)
                    Case EditorMode.Edit
                        Await SubmitEdit(message)
                    Case EditorMode.Message
                        Await SubmitMessage(message)
                End Select
            End Function)
    End Sub

    Private Async Function SubmitThread(message As String) As Task
        Dim forum = SelectedForum()
        If forum Is Nothing Then
            Main.ShowToast("Bitte wähle ein Forum.", ToastKind.Info)
            Return
        End If
        If subjectBox.Text.Trim().Length = 0 Then
            Main.ShowToast("Bitte gib einen Betreff ein.", ToastKind.Info)
            subjectBox.Focus()
            Return
        End If
        Dim result = Await Api.PostAsync("thread/create", New Dictionary(Of String, Object) From {
            {"fid", forum.Fid}, {"subject", subjectBox.Text.Trim()}, {"message", message}, {"posthash", posthash},
            {"signature", signatureToggle.Checked}, {"close", closeToggle.Checked}, {"stick", stickToggle.Checked}})
        Drafts.Delete("thread_" & AccountKey() & "_" & forum.Fid)
        loadingContent = True
        editor.Text = ""
        loadingContent = False
        Main.ShowToast(Json.GetString(result, "message", "Thema erstellt."), ToastKind.Success)
        Main.GoBack()
        Main.OpenThread(Json.GetInt(result, "tid"))
    End Function

    Private Async Function SubmitReply(message As String) As Task
        Dim result = Await Api.PostAsync("post/reply", New Dictionary(Of String, Object) From {
            {"tid", threadId}, {"message", message}, {"subject", subjectBox.Text.Trim()}, {"posthash", posthash},
            {"signature", signatureToggle.Checked}, {"close", closeToggle.Checked}, {"stick", stickToggle.Checked}})
        FinishPost(Json.GetString(result, "message", "Antwort gesendet."), Json.GetInt(result, "pid"))
    End Function

    Private Async Function SubmitEdit(message As String) As Task
        Dim body As New Dictionary(Of String, Object) From {{"pid", postId}, {"message", message}, {"editreason", reasonBox.Text.Trim()}}
        If subjectBox.Text.Trim().Length > 0 Then body("subject") = subjectBox.Text.Trim()
        Dim result = Await Api.PostAsync("post/edit", body)
        FinishPost(Json.GetString(result, "message", "Beitrag gespeichert."), postId)
    End Function

    Private Sub FinishPost(info As String, pid As Integer)
        Drafts.Delete(draftKey)
        loadingContent = True
        editor.Text = ""
        loadingContent = False
        Main.ShowToast(info, ToastKind.Success)
        Main.GoBack()
        If Main.ThreadView.Visible Then
            Main.ThreadView.LoadThread(threadId, pid)
        Else
            Main.OpenThread(threadId, pid)
        End If
    End Sub

    Private Async Function SubmitMessage(message As String) As Task
        If recipientBox.Text.Trim().Length = 0 Then
            Main.ShowToast("Bitte gib einen Empfänger ein.", ToastKind.Info)
            recipientBox.Focus()
            Return
        End If
        If subjectBox.Text.Trim().Length = 0 Then
            Main.ShowToast("Bitte gib einen Betreff ein.", ToastKind.Info)
            subjectBox.Focus()
            Return
        End If
        Dim body As New Dictionary(Of String, Object) From {
            {"to", recipientBox.Text.Trim()}, {"subject", subjectBox.Text.Trim()}, {"message", message},
            {"signature", signatureToggle.Checked}}
        If replyToPm > 0 Then body("replyto") = replyToPm
        Dim result = Await Api.PostAsync("pm/send", body)
        Drafts.Delete(draftKey)
        loadingContent = True
        editor.Text = ""
        loadingContent = False
        Main.ShowToast(Json.GetString(result, "message", "Nachricht gesendet."), ToastKind.Success)
        Main.GoBack()
    End Function

    ' ---------- Layout ----------

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If previewCard Is Nothing OrElse preview Is Nothing Then Return

        Dim edge = ClientSize.Width - Pad
        submitButton.SetBounds(edge - 170, 30, 170, 36)
        templateButton.SetBounds(submitButton.Left - 8 - 120, 30, 120, 36)
        cancelButton.SetBounds(If(templateButton.Visible, templateButton.Left, submitButton.Left) - 8 - 110, 30, 110, 36)

        Dim top = HeaderHeight
        Dim total = ClientSize.Width - Pad * 2
        Dim previewWidth = CInt(total * 0.4)
        Dim leftWidth = total - previewWidth - 16
        Dim y = top

        If forumCombo.Visible OrElse recipientBox.Visible Then
            topLabel.SetBounds(Pad, y, leftWidth, 18)
            forumCombo.SetBounds(Pad, y + 20, leftWidth, 32)
            recipientBox.SetBounds(Pad, y + 20, leftWidth, 34)
            y += 62
        End If
        If contextLabel.Visible Then
            contextLabel.SetBounds(Pad, y, leftWidth, 22)
            y += 30
        End If
        subjectLabel.SetBounds(Pad, y, leftWidth, 18)
        If reasonBox.Visible Then
            Dim half = (leftWidth - 10) \ 2
            subjectBox.SetBounds(Pad, y + 20, half, 34)
            reasonBox.SetBounds(Pad + half + 10, y + 20, leftWidth - half - 10, 34)
        Else
            subjectBox.SetBounds(Pad, y + 20, leftWidth, 34)
        End If
        y += 64

        Dim bottom = ClientSize.Height - Pad
        Dim optionsHeight = 30
        Dim attachmentsHeight = If(attachments.Visible, 120, 0)
        Dim editorHeight = Math.Max(140, bottom - y - optionsHeight - attachmentsHeight - If(attachments.Visible, 30, 0) - 10)
        editor.SetBounds(Pad, y, leftWidth, editorHeight)
        y = editor.Bottom + 8
        If attachments.Visible Then
            attachmentsLabel.SetBounds(Pad, y, leftWidth, 18)
            attachments.SetBounds(Pad, y + 20, leftWidth, attachmentsHeight)
            y = attachments.Bottom + 8
        End If
        signatureToggle.SetBounds(Pad, y, 190, 24)
        closeToggle.SetBounds(Pad + 200, y, 170, 24)
        stickToggle.SetBounds(Pad + 380, y, 170, 24)

        previewCard.SetBounds(Pad + leftWidth + 16, top, previewWidth, bottom - top)
        previewToggle.SetBounds(previewWidth - 16 - 90, 16, 90, 24)
        preview.SetBounds(2, previewCard.ContentTop, previewWidth - 4, previewCard.Height - previewCard.ContentTop - 4)
    End Sub

End Class
