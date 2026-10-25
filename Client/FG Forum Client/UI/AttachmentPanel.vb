''' <summary>One file in the <see cref="AttachmentPanel"/>.</summary>
Friend Class UploadItem
    Public Property FileName As String = ""
    Public Property Size As Long
    Public Property Aid As Integer
    Public Property IsImage As Boolean
    Public Property Status As String = ""
    Public Property Failed As Boolean
End Class


''' <summary>
''' Uploads attachments for a new post (posthash) or an existing post (pid) and lists them.
''' Images are inserted into the text automatically with [attachment=X].
''' </summary>
Friend Class AttachmentPanel
    Inherits Panel

    Private ReadOnly main As MainForm
    Private ReadOnly list As New EntryListBox() With {.ItemHeight = 38, .EmptyText = "Dateien hineinziehen, mit Strg+V einfügen oder über die Büroklammer auswählen."}
    Private ReadOnly insertButton As New ModernButton() With {.Text = "Einfügen", .Kind = ButtonKind.Secondary}
    Private ReadOnly removeButton As New ModernButton() With {.Text = "Entfernen", .Kind = ButtonKind.Ghost, .IconGlyph = Theme.IconDelete}

    Private fid As Integer
    Private tid As Integer
    Private pid As Integer
    Private posthash As String = ""
    Private uploading As Integer

    ''' <summary>The panel wants "[attachment=X]" inserted at the cursor.</summary>
    Public Event InsertRequested As EventHandler(Of FilesEventArgs)

    Public Sub New(owner As MainForm)
        main = owner
        BackColor = Theme.Surface
        list.AllowDrop = True
        Controls.AddRange(New Control() {list, insertButton, removeButton})
        AddHandler insertButton.Click, AddressOf Insert_Click
        AddHandler removeButton.Click, AddressOf Remove_Click
        AddHandler list.DoubleClick, AddressOf Insert_Click
        AddHandler list.SelectedIndexChanged, Sub(s, e) UpdateButtons()
        AddHandler list.DragEnter, Sub(s, e) e.Effect = If(e.Data.GetDataPresent(DataFormats.FileDrop), DragDropEffects.Copy, DragDropEffects.None)
        AddHandler list.DragDrop,
            Sub(s, e)
                Dim dropped = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
                If dropped IsNot Nothing Then AddFiles(dropped.ToList())
            End Sub
        UpdateButtons()
    End Sub

    ''' <summary>New post: files belong to the posthash until the post is sent.</summary>
    Public Sub ConfigureNew(forumId As Integer, threadId As Integer, hash As String)
        fid = forumId
        tid = threadId
        pid = 0
        posthash = hash
        list.Items.Clear()
        UpdateButtons()
    End Sub

    ''' <summary>Changes the forum for new uploads without removing the list (new thread).</summary>
    Public Sub ConfigureNewKeep(forumId As Integer)
        If pid = 0 Then fid = forumId
    End Sub

    ''' <summary>Edit: files are attached to the existing post right away.</summary>
    Public Sub ConfigureEdit(postId As Integer, existing As IEnumerable(Of AttachmentInfo))
        fid = 0
        tid = 0
        pid = postId
        posthash = ""
        list.Items.Clear()
        For Each attachment As AttachmentInfo In existing
            AddEntry(New UploadItem With {
                .FileName = attachment.FileName, .Size = attachment.FileSize, .Aid = attachment.Aid,
                .IsImage = attachment.IsImage, .Status = "angehängt"})
        Next
        UpdateButtons()
    End Sub

    Public ReadOnly Property IsUploading As Boolean
        Get
            Return uploading > 0
        End Get
    End Property

    Public ReadOnly Property Count As Integer
        Get
            Return list.Items.Count
        End Get
    End Property

    Private Function AddEntry(item As UploadItem) As ListEntry
        Dim entry As New ListEntry With {.Tag = item}
        UpdateEntry(entry)
        list.Items.Add(entry)
        Return entry
    End Function

    Private Sub UpdateEntry(entry As ListEntry)
        Dim item = DirectCast(entry.Tag, UploadItem)
        entry.Title = item.FileName
        entry.Subtitle = Fmt.FileSize(item.Size) & If(item.Aid > 0, "  ·  [attachment=" & item.Aid & "]", "")
        entry.Detail = item.Status
        entry.Dimmed = item.Failed
        entry.Badge = If(item.Failed, "Fehler", If(item.IsImage, "Bild", ""))
        entry.BadgeColor = If(item.Failed, Theme.Danger, Theme.Info)
        list.Invalidate()
    End Sub

    ''' <summary>Checks and uploads the files one after another.</summary>
    Public Async Sub AddFiles(files As List(Of String))
        If main.Api Is Nothing OrElse main.CurrentUser Is Nothing Then Return
        For Each filePath As String In files
            Dim info As New FileInfo(filePath)
            If Not info.Exists Then Continue For
            Dim item As New UploadItem With {.FileName = info.Name, .Size = info.Length, .IsImage = MimeTypes.IsImage(info.Name), .Status = "wartet …"}
            Dim entry = AddEntry(item)

            Dim problem = main.CurrentUser.CheckUpload(info.Name, info.Length)
            If problem.Length > 0 Then
                item.Failed = True
                item.Status = "nicht erlaubt"
                UpdateEntry(entry)
                main.ShowToast(info.Name & ": " & problem, ToastKind.Danger)
                Continue For
            End If

            uploading += 1
            UpdateButtons()
            Try
                Dim fields As New Dictionary(Of String, Object)()
                If pid > 0 Then
                    fields("pid") = pid
                Else
                    fields("posthash") = posthash
                    If fid > 0 Then fields("fid") = fid
                    If tid > 0 Then fields("tid") = tid
                End If
                Dim uploadProgress As New Progress(Of Double)(
                    Sub(percent)
                        item.Status = "lädt " & CInt(percent) & " %"
                        UpdateEntry(entry)
                    End Sub)
                Dim result = Await main.Api.UploadAsync("attachment/upload", fields, filePath, info.Name, uploadProgress)
                item.Aid = Json.GetInt(result, "aid")
                item.IsImage = Json.GetBool(result, "is_image")
                item.Status = "hochgeladen"
                UpdateEntry(entry)
                If item.IsImage Then RaiseEvent InsertRequested(Me, New FilesEventArgs(New List(Of String) From {"[attachment=" & item.Aid & "]"}))
            Catch ex As ApiException
                item.Failed = True
                item.Status = "fehlgeschlagen"
                UpdateEntry(entry)
                main.ShowToast(info.Name & ": " & ex.Message, ToastKind.Danger)
            Finally
                uploading -= 1
                UpdateButtons()
            End Try
        Next
    End Sub

    Private Sub Insert_Click(sender As Object, e As EventArgs)
        Dim entry = TryCast(list.SelectedItem, ListEntry)
        If entry Is Nothing Then Return
        Dim item = DirectCast(entry.Tag, UploadItem)
        If item.Aid <= 0 Then Return
        RaiseEvent InsertRequested(Me, New FilesEventArgs(New List(Of String) From {"[attachment=" & item.Aid & "]"}))
    End Sub

    Private Async Sub Remove_Click(sender As Object, e As EventArgs)
        Dim entry = TryCast(list.SelectedItem, ListEntry)
        If entry Is Nothing Then Return
        Dim item = DirectCast(entry.Tag, UploadItem)
        If item.Aid > 0 AndAlso main.Api IsNot Nothing Then
            Try
                Await main.Api.PostAsync("attachment/delete", New Dictionary(Of String, Object) From {{"aid", item.Aid}})
            Catch ex As Exception
                main.ShowError(ex)
                Return
            End Try
        End If
        list.Items.Remove(entry)
        UpdateButtons()
    End Sub

    Private Sub UpdateButtons()
        Dim entry = TryCast(list.SelectedItem, ListEntry)
        Dim item = If(entry Is Nothing, Nothing, TryCast(entry.Tag, UploadItem))
        insertButton.Enabled = item IsNot Nothing AndAlso item.Aid > 0
        removeButton.Enabled = item IsNot Nothing AndAlso uploading = 0
    End Sub

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If removeButton Is Nothing Then Return
        Dim buttonWidth = 112
        list.SetBounds(0, 0, Math.Max(10, Width - buttonWidth - 10), Height)
        insertButton.SetBounds(Width - buttonWidth, 0, buttonWidth, 32)
        removeButton.SetBounds(Width - buttonWidth, 40, buttonWidth, 32)
    End Sub

End Class
