''' <summary>Forum list on the left, threads of the selected forum on the right.</summary>
Friend Class ForumsPage
    Inherits PageBase

    Private ReadOnly forumsCard As New CardPanel() With {.Title = "Foren"}
    Private ReadOnly forumList As New EntryListBox() With {.ItemHeight = 52, .EmptyText = "Keine Foren sichtbar."}
    Private ReadOnly threadsCard As New CardPanel() With {.Title = "Themen"}
    Private ReadOnly threadList As New EntryListBox() With {.ItemHeight = 56, .EmptyText = "Wähle links ein Forum."}
    Private ReadOnly newThreadButton As ModernButton = MakeButton("Neues Thema", ButtonKind.Primary, Theme.IconAdd)
    Private ReadOnly refreshButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconRefresh)
    Private ReadOnly previousButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconBack)
    Private ReadOnly nextButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconForward)
    Private ReadOnly pageLabel As Label = MakeLabel("", True, True)

    Private forums As New List(Of ForumInfo)()
    Private currentFid As Integer
    Private pendingFid As Integer
    Private currentPage As Integer = 1
    Private pageCount As Integer = 1
    Private loadingForums As Boolean

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Foren", "Alle Bereiche, die du sehen darfst")
        pageLabel.TextAlign = ContentAlignment.MiddleCenter

        forumsCard.Controls.Add(forumList)
        threadsCard.Controls.AddRange(New Control() {newThreadButton, refreshButton, threadList, previousButton, pageLabel, nextButton})
        Controls.AddRange(New Control() {forumsCard, threadsCard})

        AddHandler forumList.SelectedIndexChanged, AddressOf Forum_Selected
        AddHandler threadList.DoubleClick, AddressOf OpenSelectedThread
        AddHandler threadList.KeyDown,
            Sub(s, e)
                If e.KeyCode = Keys.Enter Then OpenSelectedThread(s, e)
            End Sub
        AddHandler newThreadButton.Click, Sub(s, e) Main.OpenNewThread(currentFid)
        AddHandler refreshButton.Click, Sub(s, e) LoadThreadsAsync()
        AddHandler previousButton.Click,
            Sub(s, e)
                If currentPage > 1 Then
                    currentPage -= 1
                    LoadThreadsAsync()
                End If
            End Sub
        AddHandler nextButton.Click,
            Sub(s, e)
                If currentPage < pageCount Then
                    currentPage += 1
                    LoadThreadsAsync()
                End If
            End Sub
        newThreadButton.Enabled = False
    End Sub

    Friend Overrides Sub OnShown()
        If Not RequireConnection() Then Return
        If forums.Count = 0 Then
            LoadForumsAsync()
        ElseIf currentFid > 0 Then
            LoadThreadsAsync()
        End If
    End Sub

    Friend Overrides Sub RefreshPage()
        forums.Clear()
        OnShown()
    End Sub

    Friend Overrides Sub OnAccountChanged()
        forums.Clear()
        forumList.Items.Clear()
        threadList.Items.Clear()
        currentFid = 0
        threadsCard.Title = "Themen"
        threadsCard.Subtitle = ""
    End Sub

    ''' <summary>Forums the user may start threads in (for the editor).</summary>
    Friend Async Function GetForumsAsync() As Task(Of List(Of ForumInfo))
        If forums.Count = 0 AndAlso IsConnected Then
            Dim data = Await Api.GetAsync("forums")
            forums = Json.GetArray(data, "forums").Select(Function(f) ForumInfo.From(f)).ToList()
        End If
        Return forums
    End Function

    Friend Sub SelectForum(fid As Integer)
        pendingFid = fid
        If forums.Count > 0 Then SelectPending()
    End Sub

    Private Sub SelectPending()
        If pendingFid <= 0 Then Return
        For i = 0 To forumList.Items.Count - 1
            Dim entry = TryCast(forumList.Items(i), ListEntry)
            If entry IsNot Nothing AndAlso TypeOf entry.Tag Is ForumInfo AndAlso DirectCast(entry.Tag, ForumInfo).Fid = pendingFid Then
                forumList.SelectedIndex = i
                Exit For
            End If
        Next
        pendingFid = 0
    End Sub

    Private Async Sub LoadForumsAsync()
        If loadingForums Then Return
        loadingForums = True
        Try
            forums.Clear()
            Await GetForumsAsync()
            forumList.BeginUpdate()
            forumList.Items.Clear()
            For Each forum As ForumInfo In forums
                If forum.IsCategory AndAlso forum.Depth = 0 Then
                    forumList.Items.Add(New ListEntry With {.Title = forum.Name, .IsHeader = True, .Tag = forum})
                    Continue For
                End If
                Dim indent = Math.Max(0, forum.Depth - 1)
                Dim info = If(forum.LinkTo.Length > 0, "Link: " & forum.LinkTo,
                              Fmt.Number(forum.Threads) & " Themen · " & Fmt.Number(forum.Posts) & " Beiträge" &
                              If(forum.LastPostUser.Length > 0, " · zuletzt " & forum.LastPostUser, ""))
                forumList.Items.Add(New ListEntry With {
                    .Title = forum.Name,
                    .Subtitle = info,
                    .Detail = If(forum.LastPostDate > 0, Fmt.Relative(forum.LastPostDate), ""),
                    .Badge = If(Not forum.Open, "geschlossen", ""),
                    .BadgeColor = Theme.Warning,
                    .Indent = indent,
                    .Tag = forum})
            Next
            forumList.EndUpdate()
            forumsCard.Subtitle = forums.Where(Function(f) Not f.IsCategory).Count() & " Foren"

            If pendingFid > 0 Then
                SelectPending()
            ElseIf currentFid > 0 Then
                pendingFid = currentFid
                SelectPending()
            Else
                ' Select the first real forum
                For i = 0 To forumList.Items.Count - 1
                    Dim entry = DirectCast(forumList.Items(i), ListEntry)
                    If Not entry.IsHeader AndAlso DirectCast(entry.Tag, ForumInfo).Type = "f" Then
                        forumList.SelectedIndex = i
                        Exit For
                    End If
                Next
            End If
        Catch ex As Exception
            Main.ShowError(ex)
        Finally
            loadingForums = False
        End Try
    End Sub

    Private Sub Forum_Selected(sender As Object, e As EventArgs)
        Dim entry = TryCast(forumList.SelectedItem, ListEntry)
        If entry Is Nothing OrElse entry.IsHeader Then Return
        Dim forum = DirectCast(entry.Tag, ForumInfo)
        If forum.LinkTo.Length > 0 Then
            AppInfo.OpenUrl(forum.LinkTo)
            Return
        End If
        If forum.Fid = currentFid AndAlso threadList.Items.Count > 0 Then Return
        currentFid = forum.Fid
        currentPage = 1
        threadsCard.Title = forum.Name
        threadsCard.Subtitle = If(forum.Description.Length > 0, forum.Description, Fmt.Number(forum.Threads) & " Themen")
        newThreadButton.Enabled = forum.CanPostThreads
        LoadThreadsAsync()
    End Sub

    Private Async Sub LoadThreadsAsync()
        If currentFid <= 0 OrElse Not IsConnected Then Return
        Try
            Dim data = Await Api.GetAsync("threads", New Dictionary(Of String, Object) From {
                {"fid", currentFid}, {"page", currentPage}, {"perpage", 30}})
            Dim forumData = Json.GetObject(data, "forum")
            newThreadButton.Enabled = Json.GetBool(Json.GetObject(forumData, "can"), "post_threads")
            currentPage = Json.GetInt(data, "page", 1)
            pageCount = Json.GetInt(data, "pages", 1)
            pageLabel.Text = "Seite " & currentPage & " von " & pageCount
            previousButton.Enabled = currentPage > 1
            nextButton.Enabled = currentPage < pageCount

            threadList.BeginUpdate()
            threadList.Items.Clear()
            For Each item As Object In Json.GetArray(data, "threads")
                Dim thread = ThreadInfo.From(item)
                Dim badge = ""
                Dim badgeColor = Theme.Accent
                If thread.Sticky Then
                    badge = "angepinnt"
                ElseIf thread.Closed Then
                    badge = "geschlossen"
                    badgeColor = Theme.Warning
                ElseIf thread.Visible = 0 Then
                    badge = "wartet auf Freigabe"
                    badgeColor = Theme.Warning
                End If
                If thread.Sticky AndAlso thread.Closed Then badge = "angepinnt · geschlossen"
                threadList.Items.Add(New ListEntry With {
                    .Title = thread.DisplaySubject,
                    .Subtitle = "von " & thread.Username & " · " & Fmt.Number(thread.Replies) & " Antworten · " & Fmt.Number(thread.Views) & " Aufrufe" &
                                If(thread.Replies > 0, " · zuletzt " & thread.LastPoster, "") &
                                If(thread.Attachments > 0, " · " & thread.Attachments & " Anhang/Anhänge", ""),
                    .Detail = Fmt.Relative(thread.LastPost),
                    .Badge = badge,
                    .BadgeColor = badgeColor,
                    .Dimmed = thread.Closed,
                    .Tag = thread})
            Next
            threadList.EndUpdate()
            threadList.EmptyText = "In diesem Forum gibt es noch keine Themen."
        Catch ex As Exception
            Main.ShowError(ex)
        End Try
    End Sub

    Private Sub OpenSelectedThread(sender As Object, e As EventArgs)
        Dim entry = TryCast(threadList.SelectedItem, ListEntry)
        If entry Is Nothing Then Return
        Main.OpenThread(DirectCast(entry.Tag, ThreadInfo).Tid)
    End Sub

    ' ---------- Layout ----------

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If threadsCard Is Nothing OrElse nextButton Is Nothing Then Return

        Dim top = HeaderHeight
        Dim areaHeight = Math.Max(300, ClientSize.Height - top - Pad)
        Dim leftWidth = Math.Min(380, CInt((ClientSize.Width - Pad * 2) * 0.36))
        forumsCard.SetBounds(Pad, top, leftWidth, areaHeight)
        forumList.SetBounds(10, forumsCard.ContentTop, leftWidth - 20, areaHeight - forumsCard.ContentTop - 10)

        Dim rightLeft = Pad + leftWidth + 12
        Dim rightWidth = ClientSize.Width - rightLeft - Pad
        threadsCard.SetBounds(rightLeft, top, rightWidth, areaHeight)
        refreshButton.SetBounds(rightWidth - 16 - 36, 12, 36, 34)
        newThreadButton.SetBounds(refreshButton.Left - 8 - 150, 12, 150, 34)
        Dim footer = 52
        threadList.SetBounds(10, threadsCard.ContentTop + 4, rightWidth - 20, areaHeight - threadsCard.ContentTop - footer - 6)
        previousButton.SetBounds(16, areaHeight - 44, 40, 32)
        nextButton.SetBounds(rightWidth - 16 - 40, areaHeight - 44, 40, 32)
        pageLabel.SetBounds(64, areaHeight - 44, rightWidth - 128, 32)
    End Sub

End Class
