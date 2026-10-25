Imports System.Globalization
Imports System.Text.RegularExpressions

' =====================================================================
'  Data classes for the API answers. Each has a From(...) function that
'  reads the JSON dictionary, so the pages never touch raw JSON.
' =====================================================================

Friend Class UserBrief
    Public Property Uid As Integer
    Public Property Username As String = ""
    Public Property UsernameHtml As String = ""
    Public Property Avatar As String = ""
    Public Property Usergroup As Integer
    Public Property Usertitle As String = ""
    Public Property Postnum As Integer

    Public Shared Function From(obj As Object) As UserBrief
        Dim user As New UserBrief()
        If obj Is Nothing Then Return user
        user.Uid = Json.GetInt(obj, "uid")
        user.Username = Json.GetString(obj, "username")
        user.UsernameHtml = Json.GetString(obj, "username_html", Net.WebUtility.HtmlEncode(user.Username))
        user.Avatar = Json.GetString(obj, "avatar")
        user.Usergroup = Json.GetInt(obj, "usergroup")
        user.Usertitle = Json.GetString(obj, "usertitle")
        user.Postnum = Json.GetInt(obj, "postnum")
        Return user
    End Function
End Class


Friend Class MeInfo
    Public Property User As New UserBrief()
    Public Property Email As String = ""
    Public Property UnreadPms As Integer
    Public Property TotalPms As Integer
    Public Property CanUsePms As Boolean
    Public Property CanSendPms As Boolean
    Public Property CanCreateKeys As Boolean
    Public Property IsAdmin As Boolean
    Public Property IsModerator As Boolean
    Public Property Suspended As Boolean
    Public Property AttachmentsEnabled As Boolean
    Public Property MaxAttachments As Integer
    Public Property AttachmentTypes As New List(Of AttachmentType)()
    Public Property AuthKind As String = "session"

    Public Shared Function From(obj As Object) As MeInfo
        Dim userInfo As New MeInfo()
        userInfo.User = UserBrief.From(obj)
        userInfo.Email = Json.GetString(obj, "email")
        Dim pms = Json.GetObject(obj, "pms")
        userInfo.UnreadPms = Json.GetInt(pms, "unread")
        userInfo.TotalPms = Json.GetInt(pms, "total")
        Dim permissions = Json.GetObject(obj, "permissions")
        userInfo.CanUsePms = Json.GetBool(permissions, "use_pms")
        userInfo.CanSendPms = Json.GetBool(permissions, "send_pms")
        userInfo.CanCreateKeys = Json.GetBool(permissions, "create_keys")
        userInfo.IsAdmin = Json.GetBool(permissions, "admin")
        userInfo.IsModerator = Json.GetBool(permissions, "moderator")
        userInfo.Suspended = Json.GetBool(permissions, "suspended")
        Dim attachments = Json.GetObject(obj, "attachments")
        userInfo.AttachmentsEnabled = Json.GetBool(attachments, "enabled")
        userInfo.MaxAttachments = Json.GetInt(attachments, "max_files")
        For Each item As Object In Json.GetArray(attachments, "types")
            userInfo.AttachmentTypes.Add(New AttachmentType With {
                .Extension = Json.GetString(item, "ext").ToLowerInvariant(),
                .MaxKb = Json.GetInt(item, "max_kb")})
        Next
        userInfo.AuthKind = Json.GetString(Json.GetObject(obj, "auth"), "kind", "session")
        Return userInfo
    End Function

    ''' <summary>Checks type and size before uploading. Returns an error text or "".</summary>
    Public Function CheckUpload(fileName As String, sizeBytes As Long) As String
        If Not AttachmentsEnabled Then Return "Anhänge sind in diesem Forum abgeschaltet."
        Dim ext = IO.Path.GetExtension(fileName).TrimStart("."c).ToLowerInvariant()
        Dim attachType = AttachmentTypes.FirstOrDefault(Function(t) t.Extension = ext)
        If attachType Is Nothing Then
            Dim allowed = String.Join(", ", AttachmentTypes.Select(Function(t) t.Extension).OrderBy(Function(x) x))
            Return "Dateityp ." & ext & " ist im Forum nicht erlaubt." & If(allowed.Length > 0, Environment.NewLine & "Erlaubt: " & allowed, "")
        End If
        If attachType.MaxKb > 0 AndAlso sizeBytes > attachType.MaxKb * 1024L Then
            Return "Die Datei ist zu groß. Erlaubt sind für ." & ext & " höchstens " & Fmt.FileSize(attachType.MaxKb * 1024L) & "."
        End If
        Return ""
    End Function
End Class


Friend Class AttachmentType
    Public Property Extension As String = ""
    Public Property MaxKb As Integer
End Class


Friend Class ForumInfo
    Public Property Fid As Integer
    Public Property ParentId As Integer
    Public Property Depth As Integer
    Public Property Type As String = "f"
    Public Property Name As String = ""
    Public Property Description As String = ""
    Public Property LinkTo As String = ""
    Public Property Open As Boolean
    Public Property Threads As Integer
    Public Property Posts As Integer
    Public Property LastPostDate As Long
    Public Property LastPostSubject As String = ""
    Public Property LastPostUser As String = ""
    Public Property LastPostTid As Integer
    Public Property CanViewThreads As Boolean
    Public Property CanPostThreads As Boolean
    Public Property CanPostReplies As Boolean
    Public Property CanAttach As Boolean
    Public Property CanModerate As Boolean

    Public ReadOnly Property IsCategory As Boolean
        Get
            Return Type = "c"
        End Get
    End Property

    Public Shared Function From(obj As Object) As ForumInfo
        Dim forum As New ForumInfo With {
            .Fid = Json.GetInt(obj, "fid"),
            .ParentId = Json.GetInt(obj, "pid"),
            .Depth = Json.GetInt(obj, "depth"),
            .Type = Json.GetString(obj, "type", "f"),
            .Name = TextUtil.Decode(Json.GetString(obj, "name")),
            .Description = TextUtil.Decode(Json.GetString(obj, "description")),
            .LinkTo = Json.GetString(obj, "linkto"),
            .Open = Json.GetBool(obj, "open", True),
            .Threads = Json.GetInt(obj, "threads"),
            .Posts = Json.GetInt(obj, "posts")}
        Dim lastpost = Json.GetObject(obj, "lastpost")
        If lastpost IsNot Nothing Then
            forum.LastPostDate = Json.GetLong(lastpost, "dateline")
            forum.LastPostSubject = TextUtil.Decode(Json.GetString(lastpost, "subject"))
            forum.LastPostUser = Json.GetString(lastpost, "username")
            forum.LastPostTid = Json.GetInt(lastpost, "tid")
        End If
        Dim can = Json.GetObject(obj, "can")
        forum.CanViewThreads = Json.GetBool(can, "view_threads", True)
        forum.CanPostThreads = Json.GetBool(can, "post_threads")
        forum.CanPostReplies = Json.GetBool(can, "post_replies")
        forum.CanAttach = Json.GetBool(can, "attach")
        forum.CanModerate = Json.GetBool(can, "moderate")
        Return forum
    End Function

    Public Overrides Function ToString() As String
        Return New String(" "c, Math.Max(0, Depth - 1) * 3) & Name
    End Function
End Class


Friend Class ThreadInfo
    Public Property Tid As Integer
    Public Property Fid As Integer
    Public Property Subject As String = ""
    Public Property Prefix As String = ""
    Public Property Uid As Integer
    Public Property Username As String = ""
    Public Property Dateline As Long
    Public Property LastPost As Long
    Public Property LastPoster As String = ""
    Public Property Replies As Integer
    Public Property Views As Integer
    Public Property Sticky As Boolean
    Public Property Closed As Boolean
    Public Property Visible As Integer = 1
    Public Property Attachments As Integer
    Public Property ForumName As String = ""
    Public Property Excerpt As String = ""

    ''' <summary>Subject with prefix, as shown in the lists.</summary>
    Public ReadOnly Property DisplaySubject As String
        Get
            Return If(Prefix.Length > 0, "[" & Prefix & "] ", "") & Subject
        End Get
    End Property

    Public Shared Function From(obj As Object) As ThreadInfo
        Return New ThreadInfo With {
            .Tid = Json.GetInt(obj, "tid"),
            .Fid = Json.GetInt(obj, "fid"),
            .Subject = TextUtil.Decode(Json.GetString(obj, "subject")),
            .Prefix = TextUtil.StripTags(Json.GetString(obj, "prefix")),
            .Uid = Json.GetInt(obj, "uid"),
            .Username = Json.GetString(obj, "username"),
            .Dateline = Json.GetLong(obj, "dateline"),
            .LastPost = Json.GetLong(obj, "lastpost"),
            .LastPoster = Json.GetString(obj, "lastposter"),
            .Replies = Json.GetInt(obj, "replies"),
            .Views = Json.GetInt(obj, "views"),
            .Sticky = Json.GetBool(obj, "sticky"),
            .Closed = Json.GetBool(obj, "closed"),
            .Visible = Json.GetInt(obj, "visible", 1),
            .Attachments = Json.GetInt(obj, "attachments"),
            .ForumName = TextUtil.Decode(Json.GetString(obj, "forumname")),
            .Excerpt = Json.GetString(obj, "excerpt")}
    End Function
End Class


Friend Class AttachmentInfo
    Public Property Aid As Integer
    Public Property Pid As Integer
    Public Property FileName As String = ""
    Public Property FileType As String = ""
    Public Property FileSize As Long
    Public Property Downloads As Integer
    Public Property IsImage As Boolean
    Public Property HasThumbnail As Boolean
    Public Property Visible As Integer = 1

    Public Shared Function From(obj As Object) As AttachmentInfo
        Return New AttachmentInfo With {
            .Aid = Json.GetInt(obj, "aid"),
            .Pid = Json.GetInt(obj, "pid"),
            .FileName = Json.GetString(obj, "filename"),
            .FileType = Json.GetString(obj, "filetype"),
            .FileSize = Json.GetLong(obj, "filesize"),
            .Downloads = Json.GetInt(obj, "downloads"),
            .IsImage = Json.GetBool(obj, "is_image"),
            .HasThumbnail = Json.GetBool(obj, "thumbnail"),
            .Visible = Json.GetInt(obj, "visible", 1)}
    End Function
End Class


Friend Class PostInfo
    Public Property Pid As Integer
    Public Property Tid As Integer
    Public Property Number As Integer
    Public Property Subject As String = ""
    Public Property Author As New UserBrief()
    Public Property Dateline As Long
    Public Property EditTime As Long
    Public Property Editor As String = ""
    Public Property EditReason As String = ""
    Public Property Message As String = ""
    Public Property Html As String = ""
    Public Property Signature As String = ""
    Public Property Visible As Integer = 1
    Public Property IsFirst As Boolean
    Public Property Attachments As New List(Of AttachmentInfo)()
    Public Property CanEdit As Boolean
    Public Property CanDelete As Boolean
    Public Property CanQuote As Boolean

    Public Shared Function From(obj As Object) As PostInfo
        Dim post As New PostInfo With {
            .Pid = Json.GetInt(obj, "pid"),
            .Tid = Json.GetInt(obj, "tid"),
            .Number = Json.GetInt(obj, "number"),
            .Subject = TextUtil.Decode(Json.GetString(obj, "subject")),
            .Author = UserBrief.From(Json.GetObject(obj, "author")),
            .Dateline = Json.GetLong(obj, "dateline"),
            .EditTime = Json.GetLong(obj, "edittime"),
            .Editor = Json.GetString(obj, "editor"),
            .EditReason = Json.GetString(obj, "editreason"),
            .Message = Json.GetString(obj, "message"),
            .Html = Json.GetString(obj, "html"),
            .Signature = Json.GetString(obj, "signature"),
            .Visible = Json.GetInt(obj, "visible", 1),
            .IsFirst = Json.GetBool(obj, "is_first")}
        For Each item As Object In Json.GetArray(obj, "attachments")
            post.Attachments.Add(AttachmentInfo.From(item))
        Next
        Dim can = Json.GetObject(obj, "can")
        post.CanEdit = Json.GetBool(can, "edit")
        post.CanDelete = Json.GetBool(can, "delete")
        post.CanQuote = Json.GetBool(can, "quote")
        Return post
    End Function

    ''' <summary>MyBB quote MyCode for this post.</summary>
    Public Function QuoteMyCode() As String
        Dim body = TextUtil.RemoveQuotes(Message).Trim()
        Return "[quote=""" & Author.Username.Replace("""", "") & """ pid='" & Pid & "' dateline='" & Dateline & "']" & body & "[/quote]" & Environment.NewLine
    End Function
End Class


Friend Class ThreadPageData
    Public Property Thread As New ThreadInfo()
    Public Property ForumName As String = ""
    Public Property CanReply As Boolean
    Public Property CanModerate As Boolean
    Public Property CanClose As Boolean
    Public Property CanStick As Boolean
    Public Property CanAttach As Boolean
    Public Property Posts As New List(Of PostInfo)()
    Public Property Page As Integer = 1
    Public Property Pages As Integer = 1
    Public Property Total As Integer
    Public Property FocusPid As Integer

    Public Shared Function From(obj As Object) As ThreadPageData
        Dim threadObject = Json.GetObject(obj, "thread")
        Dim data As New ThreadPageData With {
            .Thread = ThreadInfo.From(threadObject),
            .Page = Json.GetInt(obj, "page", 1),
            .Pages = Json.GetInt(obj, "pages", 1),
            .Total = Json.GetInt(obj, "total"),
            .FocusPid = Json.GetInt(obj, "focus_pid")}
        Dim forum = Json.GetObject(threadObject, "forum")
        data.ForumName = TextUtil.Decode(Json.GetString(forum, "name"))
        data.CanAttach = Json.GetBool(Json.GetObject(forum, "can"), "attach")
        Dim can = Json.GetObject(threadObject, "can")
        data.CanReply = Json.GetBool(can, "reply")
        data.CanModerate = Json.GetBool(can, "moderate")
        data.CanClose = Json.GetBool(can, "close")
        data.CanStick = Json.GetBool(can, "stick")
        For Each item As Object In Json.GetArray(obj, "posts")
            data.Posts.Add(PostInfo.From(item))
        Next
        Return data
    End Function
End Class


''' <summary>Entry of "latest posts" and search results.</summary>
Friend Class PostListItem
    Public Property Pid As Integer
    Public Property Tid As Integer
    Public Property Fid As Integer
    Public Property Subject As String = ""
    Public Property ThreadSubject As String = ""
    Public Property Prefix As String = ""
    Public Property ForumName As String = ""
    Public Property Author As New UserBrief()
    Public Property Dateline As Long
    Public Property IsFirst As Boolean
    Public Property Replies As Integer
    Public Property Excerpt As String = ""

    Public Shared Function From(obj As Object) As PostListItem
        Return New PostListItem With {
            .Pid = Json.GetInt(obj, "pid"),
            .Tid = Json.GetInt(obj, "tid"),
            .Fid = Json.GetInt(obj, "fid"),
            .Subject = TextUtil.Decode(Json.GetString(obj, "subject")),
            .ThreadSubject = TextUtil.Decode(Json.GetString(obj, "threadsubject")),
            .Prefix = TextUtil.StripTags(Json.GetString(obj, "prefix")),
            .ForumName = TextUtil.Decode(Json.GetString(obj, "forumname")),
            .Author = UserBrief.From(Json.GetObject(obj, "author")),
            .Dateline = Json.GetLong(obj, "dateline"),
            .IsFirst = Json.GetBool(obj, "is_first"),
            .Replies = Json.GetInt(obj, "replies"),
            .Excerpt = Json.GetString(obj, "excerpt")}
    End Function
End Class


Friend Class PmItem
    Public Property Pmid As Integer
    Public Property Subject As String = ""
    Public Property From As New UserBrief()
    Public Property ToName As String = ""
    Public Property Unread As Boolean
    Public Property Dateline As Long

    Public Shared Function Parse(obj As Object) As PmItem
        Return New PmItem With {
            .Pmid = Json.GetInt(obj, "pmid"),
            .Subject = TextUtil.Decode(Json.GetString(obj, "subject")),
            .From = UserBrief.From(Json.GetObject(obj, "from")),
            .ToName = Json.GetString(Json.GetObject(obj, "to"), "username"),
            .Unread = Json.GetBool(obj, "unread"),
            .Dateline = Json.GetLong(obj, "dateline")}
    End Function
End Class


Friend Class PmMessage
    Public Property Pmid As Integer
    Public Property Subject As String = ""
    Public Property From As New UserBrief()
    Public Property Recipient As New UserBrief()
    Public Property Dateline As Long
    Public Property Folder As Integer
    Public Property Message As String = ""
    Public Property Html As String = ""

    Public Shared Function Parse(obj As Object) As PmMessage
        Return New PmMessage With {
            .Pmid = Json.GetInt(obj, "pmid"),
            .Subject = TextUtil.Decode(Json.GetString(obj, "subject")),
            .From = UserBrief.From(Json.GetObject(obj, "from")),
            .Recipient = UserBrief.From(Json.GetObject(obj, "to")),
            .Dateline = Json.GetLong(obj, "dateline"),
            .Folder = Json.GetInt(obj, "folder"),
            .Message = Json.GetString(obj, "message"),
            .Html = Json.GetString(obj, "html")}
    End Function
End Class


Friend Class AccessEntry
    Public Property Id As Integer
    Public Property Kind As String = ""
    Public Property Label As String = ""
    Public Property Hint As String = ""
    Public Property Ip As String = ""
    Public Property Created As Long
    Public Property LastUsed As Long
    Public Property IsCurrent As Boolean

    Public Shared Function From(obj As Object) As AccessEntry
        Return New AccessEntry With {
            .Id = Json.GetInt(obj, "id"),
            .Kind = Json.GetString(obj, "kind"),
            .Label = Json.GetString(obj, "label"),
            .Hint = Json.GetString(obj, "hint"),
            .Ip = Json.GetString(obj, "ip"),
            .Created = Json.GetLong(obj, "created"),
            .LastUsed = Json.GetLong(obj, "lastused"),
            .IsCurrent = Json.GetBool(obj, "current")}
    End Function
End Class


' =====================================================================
'  Formatting helpers
' =====================================================================

Friend NotInheritable Class Fmt

    Private Sub New()
    End Sub

    Private Shared ReadOnly De As CultureInfo = CultureInfo.GetCultureInfo("de-DE")

    Public Shared Function FromUnix(seconds As Long) As DateTime
        Return DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime
    End Function

    ''' <summary>"gerade eben", "vor 5 Min.", "heute, 14:03", "gestern, 09:12" or a date.</summary>
    Public Shared Function Relative(seconds As Long) As String
        If seconds <= 0 Then Return ""
        Dim time = FromUnix(seconds)
        Dim diff = DateTime.Now - time
        If diff.TotalSeconds < 60 Then Return "gerade eben"
        If diff.TotalMinutes < 60 Then Return "vor " & CInt(Math.Floor(diff.TotalMinutes)) & " Min."
        If time.Date = DateTime.Today Then Return "heute, " & time.ToString("HH:mm", De)
        If time.Date = DateTime.Today.AddDays(-1) Then Return "gestern, " & time.ToString("HH:mm", De)
        If time.Year = DateTime.Today.Year Then Return time.ToString("d. MMM, HH:mm", De)
        Return time.ToString("dd.MM.yyyy", De)
    End Function

    Public Shared Function FullDate(seconds As Long) As String
        If seconds <= 0 Then Return ""
        Return FromUnix(seconds).ToString("dd.MM.yyyy, HH:mm", De) & " Uhr"
    End Function

    Public Shared Function FileSize(bytes As Long) As String
        If bytes >= 1024L * 1024 Then Return (bytes / 1024.0 / 1024.0).ToString("0.0", De) & " MB"
        If bytes >= 1024 Then Return (bytes / 1024.0).ToString("0", De) & " KB"
        Return bytes & " Bytes"
    End Function

    Public Shared Function Number(value As Integer) As String
        Return value.ToString("N0", De)
    End Function

End Class


Friend NotInheritable Class TextUtil

    Private Sub New()
    End Sub

    ''' <summary>MyBB sends some names HTML-encoded (e.g. &amp;).</summary>
    Public Shared Function Decode(value As String) As String
        If String.IsNullOrEmpty(value) Then Return ""
        Return Net.WebUtility.HtmlDecode(value)
    End Function

    Public Shared Function StripTags(value As String) As String
        If String.IsNullOrEmpty(value) Then Return ""
        Return Decode(Regex.Replace(value, "<[^>]+>", "")).Trim()
    End Function

    Public Shared Function Html(value As String) As String
        Return Net.WebUtility.HtmlEncode(If(value, ""))
    End Function

    ''' <summary>Removes nested quotes, so quoting a quote does not grow forever.</summary>
    Public Shared Function RemoveQuotes(message As String) As String
        Dim result = If(message, "")
        For i = 1 To 5
            Dim before = result
            result = Regex.Replace(result, "\[quote(=[^\]]*)?\](?:(?!\[quote).)*?\[/quote\]\s*", "", RegexOptions.IgnoreCase Or RegexOptions.Singleline)
            If result = before Then Exit For
        Next
        Return result
    End Function

End Class
