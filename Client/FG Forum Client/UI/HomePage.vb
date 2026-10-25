''' <summary>Start page: newest posts, who is online, statistics and messages.</summary>
Friend Class HomePage
    Inherits PageBase

    Private ReadOnly newThreadButton As ModernButton = MakeButton("Neues Thema", ButtonKind.Primary, Theme.IconAdd)
    Private ReadOnly releaseButton As ModernButton = MakeButton("Tool veröffentlichen", ButtonKind.Secondary, Theme.IconRocket)
    Private ReadOnly refreshButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconRefresh)

    Private ReadOnly statCards As New List(Of StatCard)()
    Private ReadOnly latestCard As New CardPanel() With {.Title = "Neueste Beiträge"}
    Private ReadOnly modeCombo As New DarkComboBox()
    Private ReadOnly latestList As New EntryListBox() With {.ItemHeight = 58, .EmptyText = "Noch keine Beiträge."}
    Private ReadOnly onlineCard As New CardPanel() With {.Title = "Gerade online"}
    Private ReadOnly onlineList As New EntryListBox() With {.ItemHeight = 40, .EmptyText = "Niemand online."}
    Private ReadOnly messagesCard As New CardPanel() With {.Title = "Private Nachrichten"}
    Private ReadOnly messagesLabel As Label = MakeLabel("", False)
    Private ReadOnly messagesButton As ModernButton = MakeButton("Zum Posteingang", ButtonKind.Secondary, Theme.IconMail)

    Private loadingData As Boolean
    ''' <summary>Posts newer than this are marked as new.</summary>
    Private newSincePid As Integer
    Private loadedOnce As Boolean

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Start", "Die neuesten Beiträge aus deinem Forum")

        For Each caption As String In New String() {"Beiträge", "Themen", "Mitglieder", "Online"}
            Dim card As New StatCard() With {.Caption = caption}
            statCards.Add(card)
            Controls.Add(card)
        Next

        modeCombo.Items.AddRange(New Object() {"Beiträge", "Themen"})
        modeCombo.SelectedIndex = 0
        latestCard.Controls.AddRange(New Control() {modeCombo, latestList})
        onlineCard.Controls.Add(onlineList)
        messagesCard.Controls.AddRange(New Control() {messagesLabel, messagesButton})
        Controls.AddRange(New Control() {newThreadButton, releaseButton, refreshButton, latestCard, onlineCard, messagesCard})

        AddHandler refreshButton.Click, Sub(s, e) RefreshPage()
        AddHandler modeCombo.SelectedIndexChanged, Sub(s, e) LoadLatestAsync()
        AddHandler latestList.DoubleClick, AddressOf OpenSelected
        AddHandler latestList.KeyDown,
            Sub(s, e)
                If e.KeyCode = Keys.Enter Then OpenSelected(s, e)
            End Sub
        AddHandler onlineList.DoubleClick,
            Sub(s, e)
                Dim entry = TryCast(onlineList.SelectedItem, ListEntry)
                If entry IsNot Nothing Then Main.OpenUser(CInt(entry.Tag))
            End Sub
        AddHandler messagesButton.Click, Sub(s, e) Main.ShowPage(Main.MessagesView)
        AddHandler newThreadButton.Click, Sub(s, e) Main.OpenNewThread(0)
        AddHandler releaseButton.Click, Sub(s, e) Main.EditorView.StartReleaseFromHome(Me)
    End Sub

    Friend Overrides Sub OnShown()
        If Not RequireConnection() Then Return
        SetHeading("Hallo " & Main.CurrentUser.User.Username & "!", "Neues aus " & If(Main.Store.Active.ForumName.Length > 0, Main.Store.Active.ForumName, Main.Store.Active.ForumUrl))
        If Not loadedOnce Then
            newSincePid = Main.Store.Active.LastSeenPid
        End If
        LoadAllAsync()
    End Sub

    Friend Overrides Sub OnAccountChanged()
        loadedOnce = False
        latestList.Items.Clear()
        onlineList.Items.Clear()
    End Sub

    ''' <summary>Called by the notification check: posts after this id get a dot.</summary>
    Friend Sub MarkNewPosts(sinceId As Integer)
        If newSincePid = 0 OrElse sinceId < newSincePid Then newSincePid = sinceId
        If Visible Then LoadLatestAsync()
    End Sub

    Private Async Sub LoadAllAsync()
        If loadingData Then Return
        loadingData = True
        Try
            Await Task.WhenAll(LoadLatestTask(), LoadOnlineTask())
            UpdateMessages()
            loadedOnce = True
        Catch ex As Exception
            Main.ShowError(ex)
        Finally
            loadingData = False
        End Try
    End Sub

    Private Async Sub LoadLatestAsync()
        Try
            Await LoadLatestTask()
        Catch ex As Exception
            Main.ShowError(ex)
        End Try
    End Sub

    Private Async Function LoadLatestTask() As Task
        If Not IsConnected Then Return
        Dim threadsMode = modeCombo.SelectedIndex = 1
        latestCard.Title = If(threadsMode, "Neueste Themen", "Neueste Beiträge")
        Dim query As New Dictionary(Of String, Object) From {{"limit", Main.Store.Settings.LatestCount}}
        If threadsMode Then query("type") = "threads"
        Dim data = Await Api.GetAsync("latest", query)

        latestList.BeginUpdate()
        latestList.Items.Clear()
        Dim newest = 0
        For Each item As Object In Json.GetArray(data, "items")
            If threadsMode Then
                Dim thread = ThreadInfo.From(item)
                latestList.Items.Add(New ListEntry With {
                    .Title = thread.DisplaySubject,
                    .Subtitle = thread.Username & " in " & thread.ForumName & If(thread.Excerpt.Length > 0, "  ·  " & thread.Excerpt, ""),
                    .Detail = Fmt.Relative(thread.Dateline),
                    .Badge = If(thread.Replies > 0, thread.Replies & If(thread.Replies = 1, " Antwort", " Antworten"), ""),
                    .BadgeColor = Theme.Info,
                    .Tag = New Integer() {thread.Tid, 0}})
            Else
                Dim post = PostListItem.From(item)
                newest = Math.Max(newest, post.Pid)
                Dim isNew = newSincePid > 0 AndAlso post.Pid > newSincePid AndAlso post.Author.Uid <> Main.CurrentUser.User.Uid
                latestList.Items.Add(New ListEntry With {
                    .Title = If(post.Prefix.Length > 0, "[" & post.Prefix & "] ", "") & post.ThreadSubject,
                    .Subtitle = post.Author.Username & If(post.IsFirst, " hat ein Thema erstellt", " hat geantwortet") & " · " & post.ForumName & If(post.Excerpt.Length > 0, "  ·  " & post.Excerpt, ""),
                    .Detail = Fmt.Relative(post.Dateline),
                    .Badge = If(isNew, "neu", ""),
                    .Marked = isNew,
                    .Tag = New Integer() {post.Tid, post.Pid}})
            End If
        Next
        latestList.EndUpdate()

        ' Everything on screen counts as seen for the notifications
        Dim account = Main.Store.Active
        If account IsNot Nothing AndAlso newest > account.LastSeenPid Then
            account.LastSeenPid = newest
            Main.Store.Save()
        End If
    End Function

    Private Async Function LoadOnlineTask() As Task
        If Not IsConnected Then Return
        Dim data = Await Api.GetAsync("online")
        onlineList.BeginUpdate()
        onlineList.Items.Clear()
        Dim users = Json.GetArray(data, "users")
        For Each item As Object In users
            Dim user = UserBrief.From(item)
            onlineList.Items.Add(New ListEntry With {
                .Title = user.Username,
                .Subtitle = If(Json.GetBool(item, "invisible"), "unsichtbar", TextUtil.StripTags(user.Usertitle)),
                .Detail = Fmt.Relative(Json.GetLong(item, "time")),
                .Tag = user.Uid})
        Next
        onlineList.EndUpdate()
        Dim guests = Json.GetInt(data, "guests")
        onlineCard.Subtitle = users.Count & " Mitglied(er), " & guests & " Gäste"

        Dim stats = Json.GetObject(data, "stats")
        statCards(0).Value = Fmt.Number(Json.GetInt(stats, "posts"))
        statCards(1).Value = Fmt.Number(Json.GetInt(stats, "threads"))
        statCards(2).Value = Fmt.Number(Json.GetInt(stats, "users"))
        statCards(2).Note = "Neuestes: " & Json.GetString(stats, "newest_user")
        statCards(3).Value = Fmt.Number(users.Count + guests)
        statCards(3).Note = users.Count & " Mitglied(er), " & guests & " Gäste"
        PerformLayout()
    End Function

    Private Sub UpdateMessages()
        Dim user = Main.CurrentUser
        If user Is Nothing Then Return
        newThreadButton.Visible = Not user.Suspended
        releaseButton.Visible = Not user.Suspended
        If Not user.CanUsePms Then
            messagesLabel.Text = "Private Nachrichten sind für dich nicht verfügbar."
            messagesButton.Enabled = False
        ElseIf user.UnreadPms > 0 Then
            messagesLabel.Text = "Du hast " & user.UnreadPms & " ungelesene Nachricht(en)."
            messagesLabel.ForeColor = Theme.Accent
            messagesButton.Enabled = True
        Else
            messagesLabel.Text = "Keine neuen Nachrichten. Insgesamt " & user.TotalPms & " im Postfach."
            messagesLabel.ForeColor = Theme.TextMain
            messagesButton.Enabled = True
        End If
    End Sub

    Private Sub OpenSelected(sender As Object, e As EventArgs)
        Dim entry = TryCast(latestList.SelectedItem, ListEntry)
        If entry Is Nothing Then Return
        Dim ids = DirectCast(entry.Tag, Integer())
        entry.Badge = ""
        entry.Marked = False
        latestList.Invalidate()
        Main.OpenThread(ids(0), ids(1))
    End Sub

    ' ---------- Layout ----------

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If messagesCard Is Nothing OrElse statCards.Count < 4 Then Return

        refreshButton.SetBounds(ClientSize.Width - Pad - 40, 30, 40, 36)
        newThreadButton.SetBounds(refreshButton.Left - 8 - 150, 30, 150, 36)
        releaseButton.SetBounds(newThreadButton.Left - 8 - 180, 30, 180, 36)

        Dim top = HeaderHeight
        Dim totalWidth = ClientSize.Width - Pad * 2
        Dim cardWidth = (totalWidth - 36) \ 4
        For i = 0 To 3
            statCards(i).SetBounds(Pad + i * (cardWidth + 12), top, If(i = 3, totalWidth - 3 * (cardWidth + 12), cardWidth), 86)
        Next

        Dim lowerTop = top + 98
        Dim lowerHeight = Math.Max(260, ClientSize.Height - lowerTop - Pad)
        Dim rightWidth = Math.Min(340, CInt(totalWidth * 0.32))
        Dim leftWidth = totalWidth - rightWidth - 12
        latestCard.SetBounds(Pad, lowerTop, leftWidth, lowerHeight)
        modeCombo.SetBounds(leftWidth - 16 - 150, 10, 150, 32)
        latestList.SetBounds(10, latestCard.ContentTop, leftWidth - 20, lowerHeight - latestCard.ContentTop - 10)

        Dim rightLeft = Pad + leftWidth + 12
        Dim messagesHeight = 140
        onlineCard.SetBounds(rightLeft, lowerTop, rightWidth, lowerHeight - messagesHeight - 12)
        onlineList.SetBounds(10, onlineCard.ContentTop, rightWidth - 20, onlineCard.Height - onlineCard.ContentTop - 10)
        messagesCard.SetBounds(rightLeft, onlineCard.Bottom + 12, rightWidth, messagesHeight)
        messagesLabel.SetBounds(16, messagesCard.ContentTop, rightWidth - 32, 40)
        messagesButton.SetBounds(16, messagesHeight - 16 - 34, rightWidth - 32, 34)
    End Sub

End Class


''' <summary>Small card with a big number.</summary>
Friend Class StatCard
    Inherits PaintedControl

    Private _value As String = "–"
    Private _note As String = ""

    Public Property Caption As String = ""

    Public Property Value As String
        Get
            Return _value
        End Get
        Set(v As String)
            _value = If(v, "")
            Invalidate()
        End Set
    End Property

    Public Property Note As String
        Get
            Return _note
        End Get
        Set(v As String)
            _note = If(v, "")
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        Theme.FillRounded(g, New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F), 10, Theme.Surface, Theme.Border)
        TextRenderer.DrawText(g, Caption, Theme.SmallFont, New Point(16, 12), Theme.TextMuted, TextFormatFlags.NoPadding)
        TextRenderer.DrawText(g, _value, Theme.TitleFont, New Point(14, 32), Theme.TextMain, TextFormatFlags.NoPadding)
        If _note.Length > 0 Then
            TextRenderer.DrawText(g, _note, Theme.SmallFont, New Rectangle(16, 64, Width - 28, 16), Theme.TextDim,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        End If
    End Sub

End Class
