Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Threading

''' <summary>Error answer of the forum API (or a connection problem).</summary>
Friend Class ApiException
    Inherits Exception

    Public ReadOnly Property Code As String
    Public ReadOnly Property Status As Integer
    Public ReadOnly Property Details As List(Of String)

    Public Sub New(errorCode As String, message As String, Optional httpStatus As Integer = 0, Optional errorDetails As List(Of String) = Nothing)
        MyBase.New(message)
        Code = errorCode
        Status = httpStatus
        Details = If(errorDetails, New List(Of String)())
    End Sub

    ''' <summary>Message plus details, ready for a message box.</summary>
    Public ReadOnly Property FullMessage As String
        Get
            If Details.Count = 0 Then Return Message
            Return Message & Environment.NewLine & Environment.NewLine & "• " & String.Join(Environment.NewLine & "• ", Details)
        End Get
    End Property

    ''' <summary>The login is no longer valid (token expired or revoked).</summary>
    Public ReadOnly Property IsAuthProblem As Boolean
        Get
            Return Status = 401
        End Get
    End Property
End Class


''' <summary>
''' Talks to the FG Forum API of one forum.
''' Every call returns the "data" part of the answer or throws an <see cref="ApiException"/>.
''' </summary>
Friend Class ApiClient
    Implements IDisposable

    Private Shared ReadOnly SharedHandler As New HttpClientHandler() With {
        .AutomaticDecompression = DecompressionMethods.GZip Or DecompressionMethods.Deflate,
        .UseCookies = False
    }

    Private ReadOnly http As HttpClient
    Private ReadOnly endpoint As String

    Public ReadOnly Property ForumUrl As String
    Public Property Secret As String = ""
    Public Property SecretIsKey As Boolean

    Shared Sub New()
        ' TLS 1.2 (and 1.3 where Windows supports it) for HTTPS forums
        Try
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12 Or CType(12288, SecurityProtocolType)
        Catch
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
        End Try
    End Sub

    Public Sub New(forumAddress As String)
        ForumUrl = NormalizeForumUrl(forumAddress)
        endpoint = ForumUrl & "/api/index.php"
        http = New HttpClient(SharedHandler, False) With {.Timeout = TimeSpan.FromSeconds(60)}
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FGForumClient/" & AppInfo.Version & " (+" & AppInfo.GitHubUrl & ")")
        http.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
    End Sub

    ''' <summary>
    ''' Turns what a user types ("forum.de", "https://forum.de/index.php", ".../api/") into the board address.
    ''' </summary>
    Public Shared Function NormalizeForumUrl(address As String) As String
        Dim url = If(address, "").Trim()
        If url.Length = 0 Then Return ""
        If Not url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) AndAlso Not url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
            url = "https://" & url
        End If
        Dim queryStart = url.IndexOfAny(New Char() {"?"c, "#"c})
        If queryStart >= 0 Then url = url.Substring(0, queryStart)
        url = url.TrimEnd("/"c)
        For Each ending As String In New String() {"/api/index.php", "/api", "/index.php", "/usercp.php", "/member.php"}
            If url.EndsWith(ending, StringComparison.OrdinalIgnoreCase) Then
                url = url.Substring(0, url.Length - ending.Length)
                Exit For
            End If
        Next
        Return url.TrimEnd("/"c)
    End Function

    ' ---------- Requests ----------

    Private Function BuildUrl(route As String, query As IDictionary(Of String, Object)) As String
        Dim sb As New StringBuilder(endpoint)
        sb.Append("?route=").Append(Uri.EscapeDataString(route))
        If query IsNot Nothing Then
            For Each pair As KeyValuePair(Of String, Object) In query
                If pair.Value Is Nothing Then Continue For
                sb.Append("&").Append(Uri.EscapeDataString(pair.Key)).Append("=").Append(Uri.EscapeDataString(Convert.ToString(pair.Value, Globalization.CultureInfo.InvariantCulture)))
            Next
        End If
        Return sb.ToString()
    End Function

    Private Sub AddAuth(request As HttpRequestMessage)
        If Secret.Length = 0 Then Return
        request.Headers.Add(If(SecretIsKey, "X-FG-Key", "X-FG-Token"), Secret)
    End Sub

    Public Async Function GetAsync(route As String, Optional query As IDictionary(Of String, Object) = Nothing,
                                   Optional cancel As CancellationToken = Nothing) As Task(Of Dictionary(Of String, Object))
        Using request As New HttpRequestMessage(HttpMethod.Get, BuildUrl(route, query))
            AddAuth(request)
            Return Await SendAsync(request, cancel)
        End Using
    End Function

    Public Async Function PostAsync(route As String, body As IDictionary(Of String, Object),
                                    Optional cancel As CancellationToken = Nothing) As Task(Of Dictionary(Of String, Object))
        Using request As New HttpRequestMessage(HttpMethod.Post, BuildUrl(route, Nothing))
            AddAuth(request)
            request.Content = New StringContent(Json.Serialize(If(body, New Dictionary(Of String, Object)())), Encoding.UTF8, "application/json")
            Return Await SendAsync(request, cancel)
        End Using
    End Function

    ''' <summary>Uploads a file with progress (0-100).</summary>
    Public Async Function UploadAsync(route As String, fields As IDictionary(Of String, Object), filePath As String, fileName As String,
                                      progress As IProgress(Of Double), Optional cancel As CancellationToken = Nothing) As Task(Of Dictionary(Of String, Object))
        Using request As New HttpRequestMessage(HttpMethod.Post, BuildUrl(route, Nothing))
            AddAuth(request)
            Dim form As New MultipartFormDataContent()
            For Each pair As KeyValuePair(Of String, Object) In fields
                form.Add(New StringContent(Convert.ToString(pair.Value, Globalization.CultureInfo.InvariantCulture), Encoding.UTF8), pair.Key)
            Next
            Dim fileContent As New ProgressFileContent(filePath, progress)
            fileContent.Headers.ContentType = New MediaTypeHeaderValue(MimeTypes.FromFileName(fileName))
            form.Add(fileContent, "file", fileName)
            request.Content = form
            Return Await SendAsync(request, cancel)
        End Using
    End Function

    ''' <summary>Downloads a binary answer (attachments).</summary>
    Public Async Function DownloadAsync(route As String, query As IDictionary(Of String, Object),
                                        Optional cancel As CancellationToken = Nothing) As Task(Of Byte())
        Using request As New HttpRequestMessage(HttpMethod.Get, BuildUrl(route, query))
            AddAuth(request)
            Dim response As HttpResponseMessage
            Try
                response = Await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel)
            Catch ex As TaskCanceledException When Not cancel.IsCancellationRequested
                Throw New ApiException("timeout", "Der Server antwortet nicht (Zeitüberschreitung).")
            Catch ex As HttpRequestException
                Throw New ApiException("connection", "Keine Verbindung zum Forum: " & InnerMost(ex))
            End Try
            Using response
                Dim mediaType = If(response.Content.Headers.ContentType Is Nothing, "", response.Content.Headers.ContentType.MediaType)
                If Not response.IsSuccessStatusCode OrElse mediaType = "application/json" Then
                    Dim text = Await response.Content.ReadAsStringAsync()
                    Throw ErrorFromAnswer(text, CInt(response.StatusCode))
                End If
                Return Await response.Content.ReadAsByteArrayAsync()
            End Using
        End Using
    End Function

    ''' <summary>Downloads any public URL (avatars). Returns Nothing on errors.</summary>
    Public Async Function DownloadPublicAsync(url As String) As Task(Of Byte())
        Try
            Return Await http.GetByteArrayAsync(url)
        Catch
            Return Nothing
        End Try
    End Function

    Private Async Function SendAsync(request As HttpRequestMessage, cancel As CancellationToken) As Task(Of Dictionary(Of String, Object))
        Dim response As HttpResponseMessage
        Try
            response = Await http.SendAsync(request, cancel)
        Catch ex As TaskCanceledException When Not cancel.IsCancellationRequested
            Throw New ApiException("timeout", "Der Server antwortet nicht (Zeitüberschreitung).")
        Catch ex As HttpRequestException
            Throw New ApiException("connection", "Keine Verbindung zum Forum: " & InnerMost(ex))
        End Try

        Using response
            Dim text = Await response.Content.ReadAsStringAsync()
            Dim status = CInt(response.StatusCode)
            Dim answer As Dictionary(Of String, Object) = Nothing
            Try
                answer = TryCast(Json.Parse(text), Dictionary(Of String, Object))
            Catch
                answer = Nothing
            End Try
            If answer Is Nothing OrElse Not answer.ContainsKey("ok") Then
                Throw ErrorFromAnswer(text, status)
            End If
            If Not Json.GetBool(answer, "ok") Then
                Throw ErrorFromAnswer(text, status)
            End If
            Return If(Json.GetObject(answer, "data"), New Dictionary(Of String, Object)())
        End Using
    End Function

    Private Shared Function ErrorFromAnswer(text As String, status As Integer) As ApiException
        Try
            Dim answer = Json.Parse(text)
            Dim errorObject = Json.GetObject(answer, "error")
            If errorObject IsNot Nothing Then
                Dim details As New List(Of String)()
                For Each item As Object In Json.GetArray(errorObject, "details")
                    If item IsNot Nothing Then details.Add(item.ToString())
                Next
                Return New ApiException(Json.GetString(errorObject, "code", "error"), Json.GetString(errorObject, "message", "Unbekannter Fehler"), status, details)
            End If
        Catch
            ' Not JSON
        End Try

        If status = 404 Then
            Return New ApiException("no_api", "Unter dieser Adresse wurde keine FG Forum API gefunden (HTTP 404). Ist der Ordner ""api"" hochgeladen und das Plugin aktiviert?", status)
        End If
        If status >= 500 Then
            Return New ApiException("server_error", "Der Server meldet einen Fehler (HTTP " & status & "). Details stehen im Fehlerlog des Webservers.", status)
        End If
        Return New ApiException("bad_response", "Der Server hat keine gültige API-Antwort geschickt (HTTP " & status & ").", status)
    End Function

    Private Shared Function InnerMost(ex As Exception) As String
        Dim current = ex
        While current.InnerException IsNot Nothing
            current = current.InnerException
        End While
        Return current.Message
    End Function

    Public Shared Function NewPostHash() As String
        Return Guid.NewGuid().ToString("N")
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        http.Dispose()
    End Sub

End Class


''' <summary>File upload content that reports how much has been sent.</summary>
Friend Class ProgressFileContent
    Inherits HttpContent

    Private ReadOnly path As String
    Private ReadOnly progressReport As IProgress(Of Double)

    Public Sub New(filePath As String, progress As IProgress(Of Double))
        path = filePath
        progressReport = progress
    End Sub

    Protected Overrides Async Function SerializeToStreamAsync(stream As Stream, context As TransportContext) As Task
        Dim buffer(81919) As Byte
        Using source As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            Dim total = Math.Max(1L, source.Length)
            Dim sent As Long = 0
            Do
                Dim read = Await source.ReadAsync(buffer, 0, buffer.Length)
                If read <= 0 Then Exit Do
                Await stream.WriteAsync(buffer, 0, read)
                sent += read
                If progressReport IsNot Nothing Then progressReport.Report(sent * 100.0 / total)
            Loop
        End Using
    End Function

    Protected Overrides Function TryComputeLength(ByRef length As Long) As Boolean
        length = New FileInfo(path).Length
        Return True
    End Function

End Class


''' <summary>MIME types for uploads (MyBB checks images by content).</summary>
Friend NotInheritable Class MimeTypes

    Private Sub New()
    End Sub

    Public Shared Function FromFileName(fileName As String) As String
        Select Case Path.GetExtension(If(fileName, "")).ToLowerInvariant()
            Case ".png" : Return "image/png"
            Case ".jpg", ".jpeg", ".jpe" : Return "image/jpeg"
            Case ".gif" : Return "image/gif"
            Case ".bmp" : Return "image/bmp"
            Case ".webp" : Return "image/webp"
            Case ".zip" : Return "application/zip"
            Case ".rar" : Return "application/x-rar-compressed"
            Case ".7z" : Return "application/x-7z-compressed"
            Case ".txt", ".log", ".ini", ".cfg" : Return "text/plain"
            Case ".pdf" : Return "application/pdf"
            Case ".exe" : Return "application/x-msdownload"
            Case Else : Return "application/octet-stream"
        End Select
    End Function

    Public Shared Function IsImage(fileName As String) As Boolean
        Return FromFileName(fileName).StartsWith("image/", StringComparison.Ordinal)
    End Function

End Class
