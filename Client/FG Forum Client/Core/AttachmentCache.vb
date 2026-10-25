''' <summary>
''' Keeps downloaded attachment images in memory and puts them into the HTML view
''' (as data: URLs, because the WebBrowser control cannot send the login header itself).
''' </summary>
Friend Class AttachmentCache

    Private ReadOnly images As New Dictionary(Of String, Byte())()
    Private Const MaxEntries As Integer = 300

    Public Sub Clear()
        images.Clear()
    End Sub

    Public Async Function GetImageAsync(api As ApiClient, aid As Integer, thumb As Boolean) As Task(Of Byte())
        Dim key = aid & If(thumb, "t", "f")
        Dim data As Byte() = Nothing
        If images.TryGetValue(key, data) Then Return data

        Dim query As New Dictionary(Of String, Object) From {{"aid", aid}}
        If thumb Then query("thumb") = 1
        data = Await api.DownloadAsync("attachment", query)
        If images.Count > MaxEntries Then images.Clear()
        images(key) = data
        Return data
    End Function

    ''' <summary>Loads all waiting images of the page, one after another.</summary>
    Public Async Function FillImagesAsync(view As HtmlView, api As ApiClient) As Task
        If api Is Nothing Then Return
        For Each element As HtmlElement In view.PendingImages()
            Dim aid As Integer
            If Not Integer.TryParse(element.GetAttribute("data-aid"), aid) Then Continue For
            Dim thumb = element.GetAttribute("data-thumb") = "1"
            Try
                Dim data = Await GetImageAsync(api, aid, thumb)
                element.SetAttribute("src", ToDataUrl(data))
            Catch ex As ApiException
                element.SetAttribute("alt", "Bild konnte nicht geladen werden")
                element.SetAttribute("title", ex.Message)
            Catch
                ' Page changed meanwhile
            End Try
        Next
    End Function

    Public Shared Function ToDataUrl(data As Byte()) As String
        Return "data:" & DetectMime(data) & ";base64," & Convert.ToBase64String(data)
    End Function

    Public Shared Function DetectMime(data As Byte()) As String
        If data Is Nothing OrElse data.Length < 4 Then Return "application/octet-stream"
        If data(0) = &H89 AndAlso data(1) = &H50 Then Return "image/png"
        If data(0) = &HFF AndAlso data(1) = &HD8 Then Return "image/jpeg"
        If data(0) = &H47 AndAlso data(1) = &H49 Then Return "image/gif"
        If data(0) = &H42 AndAlso data(1) = &H4D Then Return "image/bmp"
        Return "image/png"
    End Function

End Class
