''' <summary>Search in titles and posts, optionally limited to a forum or an author.</summary>
Friend Class SearchPage
    Inherits PageBase

    Private ReadOnly filterCard As New CardPanel()
    Private ReadOnly queryBox As New DarkTextBox() With {.Placeholder = "Suchbegriff, z. B. Minecraft Server"}
    Private ReadOnly forumCombo As New DarkComboBox()
    Private ReadOnly authorBox As New DarkTextBox() With {.Placeholder = "Autor (optional)"}
    Private ReadOnly titlesToggle As New ToggleSwitch() With {.Text = "Nur in Titeln"}
    Private ReadOnly searchButton As ModernButton = MakeButton("Suchen", ButtonKind.Primary, Theme.IconSearch)

    Private ReadOnly resultsCard As New CardPanel() With {.Title = "Ergebnisse"}
    Private ReadOnly resultList As New EntryListBox() With {.ItemHeight = 58, .EmptyText = "Gib oben einen Suchbegriff ein."}
    Private ReadOnly previousButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconBack)
    Private ReadOnly nextButton As ModernButton = MakeButton("", ButtonKind.Secondary, Theme.IconForward)
    Private ReadOnly pageLabel As Label = MakeLabel("", True, True)

    Private forumIds As New List(Of Integer)()
    Private currentPage As Integer = 1
    Private pageCount As Integer = 1

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Suche", "Finde Themen und Beiträge im ganzen Forum")
        pageLabel.TextAlign = ContentAlignment.MiddleCenter
        filterCard.Controls.AddRange(New Control() {queryBox, forumCombo, authorBox, titlesToggle, searchButton})
        resultsCard.Controls.AddRange(New Control() {resultList, previousButton, pageLabel, nextButton})
        Controls.AddRange(New Control() {filterCard, resultsCard})

        AddHandler searchButton.Click,
            Sub(s, e)
                currentPage = 1
                SearchAsync()
            End Sub
        AddHandler queryBox.KeyDown,
            Sub(s, e)
                If e.KeyCode = Keys.Enter Then
                    e.SuppressKeyPress = True
                    currentPage = 1
                    SearchAsync()
                End If
            End Sub
        AddHandler resultList.DoubleClick, AddressOf OpenSelected
        AddHandler resultList.KeyDown,
            Sub(s, e)
                If e.KeyCode = Keys.Enter Then OpenSelected(s, e)
            End Sub
        AddHandler previousButton.Click,
            Sub(s, e)
                If currentPage > 1 Then
                    currentPage -= 1
                    SearchAsync()
                End If
            End Sub
        AddHandler nextButton.Click,
            Sub(s, e)
                If currentPage < pageCount Then
                    currentPage += 1
                    SearchAsync()
                End If
            End Sub
        previousButton.Enabled = False
        nextButton.Enabled = False
    End Sub

    Friend Overrides Sub OnShown()
        If Not RequireConnection() Then Return
        LoadForumsAsync()
        queryBox.Focus()
    End Sub

    Friend Overrides Sub OnAccountChanged()
        forumIds.Clear()
        forumCombo.Items.Clear()
        resultList.Items.Clear()
    End Sub

    Private Async Sub LoadForumsAsync()
        If forumIds.Count > 0 Then Return
        Try
            Dim forums = Await Main.ForumsView.GetForumsAsync()
            forumCombo.Items.Clear()
            forumIds.Clear()
            forumCombo.Items.Add("Alle Foren")
            forumIds.Add(0)
            For Each forum As ForumInfo In forums
                If forum.IsCategory OrElse forum.LinkTo.Length > 0 Then Continue For
                forumCombo.Items.Add(forum.ToString())
                forumIds.Add(forum.Fid)
            Next
            forumCombo.SelectedIndex = 0
        Catch ex As Exception
            Main.ShowError(ex)
        End Try
    End Sub

    Private Async Sub SearchAsync()
        If Not IsConnected Then Return
        Dim words = queryBox.Text.Trim()
        If words.Length < 3 Then
            Main.ShowToast("Der Suchbegriff muss mindestens 3 Zeichen lang sein.", ToastKind.Info)
            Return
        End If
        Await RunBusy(searchButton,
            Async Function()
                Dim query As New Dictionary(Of String, Object) From {{"q", words}, {"page", currentPage}}
                If titlesToggle.Checked Then query("titles_only") = 1
                Dim index = forumCombo.SelectedIndex
                If index > 0 AndAlso index < forumIds.Count Then query("fid") = forumIds(index)
                If authorBox.Text.Trim().Length > 0 Then query("author") = authorBox.Text.Trim()

                Dim data = Await Api.GetAsync("search", query)
                currentPage = Json.GetInt(data, "page", 1)
                pageCount = Json.GetInt(data, "pages", 1)
                Dim total = Json.GetInt(data, "total")
                resultsCard.Title = total & If(total = 1, " Treffer", " Treffer") & " für """ & words & """"
                pageLabel.Text = "Seite " & currentPage & " von " & pageCount
                previousButton.Enabled = currentPage > 1
                nextButton.Enabled = currentPage < pageCount

                resultList.BeginUpdate()
                resultList.Items.Clear()
                For Each item As Object In Json.GetArray(data, "items")
                    Dim post = PostListItem.From(item)
                    resultList.Items.Add(New ListEntry With {
                        .Title = If(post.Prefix.Length > 0, "[" & post.Prefix & "] ", "") & post.ThreadSubject,
                        .Subtitle = post.Author.Username & " · " & post.ForumName & If(post.Excerpt.Length > 0, "  ·  " & post.Excerpt, ""),
                        .Detail = Fmt.Relative(post.Dateline),
                        .Badge = If(post.IsFirst, "Thema", ""),
                        .Tag = post})
                Next
                resultList.EndUpdate()
                resultList.EmptyText = "Nichts gefunden. Versuch es mit einem anderen Begriff."
            End Function)
    End Sub

    Private Sub OpenSelected(sender As Object, e As EventArgs)
        Dim entry = TryCast(resultList.SelectedItem, ListEntry)
        If entry Is Nothing Then Return
        Dim post = DirectCast(entry.Tag, PostListItem)
        Main.OpenThread(post.Tid, post.Pid)
    End Sub

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If resultsCard Is Nothing OrElse nextButton Is Nothing Then Return
        Dim top = HeaderHeight
        Dim total = ClientSize.Width - Pad * 2
        filterCard.SetBounds(Pad, top, total, 110)
        Dim inner = total - 32
        Dim queryWidth = CInt(inner * 0.4)
        Dim forumWidth = CInt(inner * 0.25)
        Dim authorWidth = CInt(inner * 0.17)
        queryBox.SetBounds(16, 16, queryWidth, 36)
        forumCombo.SetBounds(queryBox.Right + 10, 18, forumWidth, 32)
        authorBox.SetBounds(forumCombo.Right + 10, 16, authorWidth, 36)
        searchButton.SetBounds(total - 16 - 130, 16, 130, 36)
        titlesToggle.SetBounds(16, 66, 200, 24)

        Dim resultsTop = filterCard.Bottom + 12
        Dim areaHeight = Math.Max(200, ClientSize.Height - resultsTop - Pad)
        resultsCard.SetBounds(Pad, resultsTop, total, areaHeight)
        resultList.SetBounds(10, resultsCard.ContentTop, total - 20, areaHeight - resultsCard.ContentTop - 56)
        previousButton.SetBounds(16, areaHeight - 44, 40, 32)
        nextButton.SetBounds(total - 56, areaHeight - 44, 40, 32)
        pageLabel.SetBounds(64, areaHeight - 44, total - 128, 32)
    End Sub

End Class
