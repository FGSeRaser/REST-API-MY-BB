''' <summary>Private messages: folders, list and reading pane.</summary>
Friend Class MessagesPage
    Inherits PageBase

    Private ReadOnly newButton As ModernButton = MakeButton("Neue Nachricht", ButtonKind.Primary, Theme.IconAdd)
    Private ReadOnly refreshButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconRefresh)

    Private ReadOnly listCard As New CardPanel() With {.Title = "Postfach"}
    Private ReadOnly folderCombo As New DarkComboBox()
    Private ReadOnly messageList As New EntryListBox() With {.ItemHeight = 54, .EmptyText = "Keine Nachrichten in diesem Ordner."}
    Private ReadOnly previousButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconBack)
    Private ReadOnly nextButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconForward)
    Private ReadOnly pageLabel As Label = MakeLabel("", True, True)

    Private ReadOnly readCard As New CardPanel()
    Private ReadOnly replyButton As ModernButton = MakeButton("Antworten", ButtonKind.Secondary, Theme.IconReply)
    Private ReadOnly deleteButton As ModernButton = MakeButton("Löschen", ButtonKind.Ghost, Theme.IconDelete)
    Private ReadOnly view As New HtmlView()

    Private folderIds As New List(Of Integer) From {1, 2, 3, 4}
    Private currentFolder As Integer = 1
    Private currentPage As Integer = 1
    Private pageCount As Integer = 1
    Private openMessage As PmMessage
    Private folderUpdating As Boolean

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Nachrichten", "Private Nachrichten an andere Mitglieder")
        pageLabel.TextAlign = ContentAlignment.MiddleCenter
        folderCombo.Items.AddRange(New Object() {"Posteingang", "Gesendet", "Entwürfe", "Papierkorb"})
        folderCombo.SelectedIndex = 0

        listCard.Controls.AddRange(New Control() {folderCombo, messageList, previousButton, pageLabel, nextButton})
        readCard.Controls.AddRange(New Control() {replyButton, deleteButton, view})
        Controls.AddRange(New Control() {newButton, refreshButton, listCard, readCard})

        AddHandler newButton.Click, Sub(s, e) Main.OpenNewMessage()
        AddHandler refreshButton.Click, Sub(s, e) RefreshPage()
        AddHandler folderCombo.SelectedIndexChanged,
            Sub(s, e)
                If folderUpdating OrElse folderCombo.SelectedIndex < 0 Then Return
                currentFolder = folderIds(folderCombo.SelectedIndex)
                currentPage = 1
                LoadListAsync()
            End Sub
        AddHandler messageList.SelectedIndexChanged, AddressOf Message_Selected
        AddHandler previousButton.Click,
            Sub(s, e)
                If currentPage > 1 Then
                    currentPage -= 1
                    LoadListAsync()
                End If
            End Sub
        AddHandler nextButton.Click,
            Sub(s, e)
                If currentPage < pageCount Then
                    currentPage += 1
                    LoadListAsync()
                End If
            End Sub
        AddHandler replyButton.Click, AddressOf Reply_Click
        AddHandler deleteButton.Click, AddressOf Delete_Click
        AddHandler view.ActionClicked,
            Sub(s, e)
                If e.Action = "user" Then Main.OpenUser(e.Id)
            End Sub
        AddHandler view.PageLoaded,
            Async Sub(s, e)
                Try
                    Await Main.Images.FillImagesAsync(view, Api)
                Catch
                End Try
            End Sub
        ShowEmptyReader()
    End Sub

    Friend Overrides Sub OnShown()
        If Not RequireConnection() Then Return
        If Not Main.CurrentUser.CanUsePms Then
            messageList.Items.Clear()
            messageList.EmptyText = "Private Nachrichten sind für dich nicht verfügbar."
            newButton.Enabled = False
            Return
        End If
        newButton.Enabled = Main.CurrentUser.CanSendPms
        LoadListAsync()
    End Sub

    Friend Overrides Sub OnAccountChanged()
        messageList.Items.Clear()
        ShowEmptyReader()
        currentFolder = 1
        currentPage = 1
    End Sub

    Private Sub ShowEmptyReader()
        openMessage = Nothing
        readCard.Title = "Nachricht"
        readCard.Subtitle = ""
        replyButton.Enabled = False
        deleteButton.Enabled = False
        view.ShowHtml(HtmlBuilder.Notice("Wähle links eine Nachricht aus."))
    End Sub

    Private Async Sub LoadListAsync()
        If Not IsConnected Then Return
        Try
            Dim data = Await Api.GetAsync("pms", New Dictionary(Of String, Object) From {{"folder", currentFolder}, {"page", currentPage}})
            currentPage = Json.GetInt(data, "page", 1)
            pageCount = Json.GetInt(data, "pages", 1)
            pageLabel.Text = "Seite " & currentPage & " von " & pageCount
            previousButton.Enabled = currentPage > 1
            nextButton.Enabled = currentPage < pageCount
            Main.UpdateUnread(Json.GetInt(data, "unread"))

            ' Own folders of the user
            Dim folders = Json.GetArray(data, "folders")
            If folders.Count > 0 AndAlso folders.Count <> folderIds.Count Then
                folderUpdating = True
                folderIds = folders.Select(Function(f) Json.GetInt(f, "id")).ToList()
                folderCombo.Items.Clear()
                For Each folder As Object In folders
                    folderCombo.Items.Add(Json.GetString(folder, "name"))
                Next
                folderCombo.SelectedIndex = Math.Max(0, folderIds.IndexOf(currentFolder))
                folderUpdating = False
            End If

            Dim sent = currentFolder = 2
            messageList.BeginUpdate()
            messageList.Items.Clear()
            For Each item As Object In Json.GetArray(data, "items")
                Dim pm = PmItem.Parse(item)
                messageList.Items.Add(New ListEntry With {
                    .Title = pm.Subject,
                    .Subtitle = If(sent, "an " & pm.ToName, "von " & pm.From.Username),
                    .Detail = Fmt.Relative(pm.Dateline),
                    .Marked = pm.Unread AndAlso Not sent,
                    .Badge = If(pm.Unread AndAlso Not sent, "neu", ""),
                    .Tag = pm})
            Next
            messageList.EndUpdate()
            listCard.Subtitle = Json.GetInt(data, "total") & " Nachricht(en)"
        Catch ex As Exception
            Main.ShowError(ex)
        End Try
    End Sub

    Private Async Sub Message_Selected(sender As Object, e As EventArgs)
        Dim entry = TryCast(messageList.SelectedItem, ListEntry)
        If entry Is Nothing Then Return
        Dim item = DirectCast(entry.Tag, PmItem)
        Try
            Dim data = Await Api.GetAsync("pm", New Dictionary(Of String, Object) From {{"pmid", item.Pmid}})
            openMessage = PmMessage.Parse(data)
            readCard.Title = openMessage.Subject
            readCard.Subtitle = "von " & openMessage.From.Username & " · " & Fmt.FullDate(openMessage.Dateline)
            replyButton.Enabled = Main.CurrentUser.CanSendPms AndAlso openMessage.From.Uid > 0 AndAlso openMessage.From.Uid <> Main.CurrentUser.User.Uid
            deleteButton.Enabled = True
            view.ShowHtml(HtmlBuilder.Message(openMessage))
            If entry.Marked Then
                entry.Marked = False
                entry.Badge = ""
                messageList.Invalidate()
                Main.UpdateUnread(Math.Max(0, Main.CurrentUser.UnreadPms - 1))
            End If
        Catch ex As Exception
            Main.ShowError(ex)
        End Try
    End Sub

    Private Sub Reply_Click(sender As Object, e As EventArgs)
        If openMessage Is Nothing Then Return
        Dim subject = openMessage.Subject
        If Not subject.StartsWith("RE:", StringComparison.OrdinalIgnoreCase) Then subject = "RE: " & subject
        Dim quote = "[quote=""" & openMessage.From.Username & """]" & TextUtil.RemoveQuotes(openMessage.Message).Trim() & "[/quote]" & Environment.NewLine
        Main.OpenNewMessage(openMessage.From.Username, subject, quote, openMessage.Pmid)
    End Sub

    Private Async Sub Delete_Click(sender As Object, e As EventArgs)
        If openMessage Is Nothing Then Return
        Dim inTrash = currentFolder = 4
        Dim question = If(inTrash, "Diese Nachricht endgültig löschen?", "Diese Nachricht in den Papierkorb verschieben?")
        If MessageBox.Show(Main, question, "Nachricht löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
        Await RunBusy(deleteButton,
            Async Function()
                Await Api.PostAsync("pm/delete", New Dictionary(Of String, Object) From {{"pmid", openMessage.Pmid}})
                Main.ShowToast(If(inTrash, "Nachricht gelöscht.", "Nachricht in den Papierkorb verschoben."), ToastKind.Success)
                ShowEmptyReader()
                LoadListAsync()
            End Function)
    End Sub

    ' ---------- Layout ----------

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If readCard Is Nothing OrElse view Is Nothing Then Return

        refreshButton.SetBounds(ClientSize.Width - Pad - 40, 30, 40, 36)
        newButton.SetBounds(refreshButton.Left - 8 - 170, 30, 170, 36)

        Dim top = HeaderHeight
        Dim areaHeight = Math.Max(300, ClientSize.Height - top - Pad)
        Dim total = ClientSize.Width - Pad * 2
        Dim listWidth = Math.Min(420, CInt(total * 0.4))
        listCard.SetBounds(Pad, top, listWidth, areaHeight)
        folderCombo.SetBounds(listWidth - 16 - 170, 10, 170, 32)
        messageList.SetBounds(10, listCard.ContentTop + 4, listWidth - 20, areaHeight - listCard.ContentTop - 56)
        previousButton.SetBounds(16, areaHeight - 44, 40, 32)
        nextButton.SetBounds(listWidth - 56, areaHeight - 44, 40, 32)
        pageLabel.SetBounds(64, areaHeight - 44, listWidth - 128, 32)

        Dim readLeft = Pad + listWidth + 12
        Dim readWidth = ClientSize.Width - readLeft - Pad
        readCard.SetBounds(readLeft, top, readWidth, areaHeight)
        deleteButton.SetBounds(readWidth - 16 - 110, 12, 110, 34)
        replyButton.SetBounds(deleteButton.Left - 8 - 130, 12, 130, 34)
        view.SetBounds(2, readCard.ContentTop + 6, readWidth - 4, areaHeight - readCard.ContentTop - 8)
    End Sub

End Class
