Imports System.Text
Imports System.Text.RegularExpressions

''' <summary>
''' Builds the HTML pages shown in the thread view, the message view and the preview.
''' Links with "fg://" are actions that the client handles itself.
''' </summary>
Friend NotInheritable Class HtmlBuilder

    Private Sub New()
    End Sub

    ''' <summary>1x1 transparent GIF used until an attachment image has been loaded.</summary>
    Public Const Placeholder As String = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"

    Private Shared ReadOnly AttachmentTag As New Regex("<span class=""fg-attachment"" data-aid=""(\d+)""></span>", RegexOptions.Compiled)

    Private Shared Function Css() As String
        Return String.Join(vbLf, New String() {
            "html, body { margin:0; padding:0; background:#141518; color:#ebecf0; font-family:'Segoe UI', Tahoma, sans-serif; font-size:14px; }",
            "body { padding:6px 14px 30px 14px; }",
            "a { color:#4da3ff; text-decoration:none; }",
            "a:hover { text-decoration:underline; }",
            ".post { background:#1d1e22; border:1px solid #32343a; border-radius:10px; margin:0 0 12px 0; }",
            ".post.focus { border-color:#4da3ff; }",
            ".post.hidden { opacity:.6; border-style:dashed; }",
            ".head { display:table; width:100%; border-bottom:1px solid #2a2c31; }",
            ".head > div { display:table-cell; vertical-align:middle; padding:10px 14px; }",
            ".head .ava { width:44px; padding-right:0; }",
            ".head .ava img { width:42px; height:42px; border-radius:21px; background:#2a2c31; display:block; }",
            ".head .who { line-height:1.35; }",
            ".head .name { font-weight:600; font-size:14px; }",
            ".head .title { color:#8c8f98; font-size:12px; }",
            ".head .meta { text-align:right; color:#8c8f98; font-size:12px; white-space:nowrap; }",
            ".head .meta .num { color:#60636c; margin-left:8px; }",
            ".body { padding:12px 16px; line-height:1.55; word-wrap:break-word; overflow:hidden; }",
            ".body img { max-width:100%; }",
            ".foot { padding:0 16px 10px 16px; font-size:12px; color:#60636c; }",
            ".actions { padding:8px 12px; border-top:1px solid #2a2c31; text-align:right; }",
            ".actions a { display:inline-block; padding:4px 10px; margin-left:4px; border-radius:6px; color:#c9cbd2; font-size:12px; border:1px solid #32343a; }",
            ".actions a:hover { background:#26282d; text-decoration:none; color:#fff; }",
            ".actions a.danger:hover { background:#e5534b; border-color:#e5534b; }",
            ".sig { border-top:1px dashed #32343a; margin:0 16px; padding:10px 0; color:#a7a9b1; font-size:12px; }",
            ".badge { display:inline-block; font-size:11px; padding:1px 7px; border-radius:8px; background:#3a2d12; color:#eeb640; margin-left:6px; }",
            "blockquote.mycode_quote { margin:8px 0; padding:8px 12px; background:#17181b; border-left:3px solid #4da3ff; border-radius:4px; color:#c9cbd2; }",
            "blockquote.mycode_quote cite { display:block; font-style:normal; font-weight:600; color:#8c8f98; font-size:12px; margin-bottom:4px; }",
            "blockquote.mycode_quote cite span { float:right; font-weight:normal; }",
            ".codeblock { margin:8px 0; background:#0f1012; border:1px solid #2a2c31; border-radius:6px; }",
            ".codeblock .title { padding:4px 10px; font-size:11px; color:#8c8f98; border-bottom:1px solid #2a2c31; }",
            ".codeblock .body { padding:8px 10px; font-family:Consolas, 'Cascadia Mono', monospace; font-size:13px; white-space:pre-wrap; max-height:420px; overflow:auto; }",
            "hr.mycode_hr { border:0; border-top:1px solid #32343a; }",
            ".attachments { padding:4px 16px 12px 16px; }",
            ".attachments .label { font-size:12px; color:#8c8f98; margin-bottom:6px; }",
            ".att-img { display:inline-block; margin:0 8px 8px 0; border:1px solid #32343a; border-radius:6px; overflow:hidden; background:#17181b; }",
            ".att-img img { display:block; min-width:60px; min-height:40px; max-width:320px; max-height:240px; }",
            ".att-inline img { max-width:100%; min-width:40px; min-height:30px; border-radius:6px; border:1px solid #32343a; background:#17181b; }",
            ".att-file { display:inline-block; margin:0 8px 8px 0; padding:7px 12px; border:1px solid #32343a; border-radius:6px; background:#17181b; color:#ebecf0; }",
            ".att-file span { color:#8c8f98; font-size:12px; margin-left:6px; }",
            ".notice { text-align:center; color:#60636c; padding:30px 10px; }",
            ".pm-head { padding:12px 16px; border-bottom:1px solid #2a2c31; }",
            ".pm-head .subject { font-size:17px; font-weight:600; }",
            ".pm-head .meta { color:#8c8f98; font-size:12px; margin-top:4px; }",
            "table { border-collapse:collapse; }",
            "td, th { border:1px solid #32343a; padding:4px 8px; }"})
    End Function

    Private Shared Function Document(bodyHtml As String) As String
        Dim sb As New StringBuilder()
        sb.Append("<!DOCTYPE html><html><head><meta charset=""utf-8""><meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">")
        sb.Append("<style>").Append(Css()).Append("</style></head><body>")
        sb.Append(bodyHtml)
        sb.Append("</body></html>")
        Return sb.ToString()
    End Function

    Public Shared Function Notice(text As String) As String
        Return Document("<div class=""notice"">" & TextUtil.Html(text) & "</div>")
    End Function

    Public Shared Function Preview(html As String) As String
        If String.IsNullOrWhiteSpace(html) Then Return Notice("Hier erscheint die Vorschau, sobald du etwas schreibst.")
        Return Document("<div class=""post""><div class=""body"">" & ReplaceAttachments(html, Nothing, New HashSet(Of Integer)()) & "</div></div>")
    End Function

    ' ---------- Thread ----------

    Public Shared Function Thread(data As ThreadPageData, showSignatures As Boolean) As String
        Dim sb As New StringBuilder()
        If data.Posts.Count = 0 Then
            sb.Append("<div class=""notice"">In diesem Thema gibt es (noch) keine sichtbaren Beiträge.</div>")
        End If
        For Each post As PostInfo In data.Posts
            AppendPost(sb, post, data, showSignatures)
        Next
        Return Document(sb.ToString())
    End Function

    Private Shared Sub AppendPost(sb As StringBuilder, post As PostInfo, data As ThreadPageData, showSignatures As Boolean)
        Dim classes = "post"
        If post.Pid = data.FocusPid Then classes &= " focus"
        If post.Visible <> 1 Then classes &= " hidden"

        sb.Append("<div class=""").Append(classes).Append(""" id=""pid").Append(post.Pid).Append(""">")

        ' Header with avatar, name and date
        sb.Append("<div class=""head"">")
        sb.Append("<div class=""ava""><a href=""fg://user/").Append(post.Author.Uid).Append(""">")
        If post.Author.Avatar.Length > 0 Then
            sb.Append("<img src=""").Append(TextUtil.Html(post.Author.Avatar)).Append(""" alt="""">")
        Else
            sb.Append("<img src=""").Append(Placeholder).Append(""" alt="""">")
        End If
        sb.Append("</a></div>")
        sb.Append("<div class=""who""><div class=""name""><a href=""fg://user/").Append(post.Author.Uid).Append(""">")
        sb.Append(post.Author.UsernameHtml).Append("</a>")
        If post.Visible = 0 Then sb.Append("<span class=""badge"">wartet auf Freigabe</span>")
        If post.Visible = -1 Then sb.Append("<span class=""badge"">gelöscht</span>")
        sb.Append("</div><div class=""title"">").Append(TextUtil.Html(TextUtil.StripTags(post.Author.Usertitle)))
        If post.Author.Postnum > 0 Then sb.Append(" · ").Append(Fmt.Number(post.Author.Postnum)).Append(" Beiträge")
        sb.Append("</div></div>")
        sb.Append("<div class=""meta"" title=""").Append(Fmt.FullDate(post.Dateline)).Append(""">").Append(Fmt.Relative(post.Dateline))
        sb.Append("<span class=""num"">#").Append(post.Number).Append("</span></div>")
        sb.Append("</div>")

        ' Message
        Dim inline As New HashSet(Of Integer)()
        sb.Append("<div class=""body"">").Append(ReplaceAttachments(post.Html, post.Attachments, inline)).Append("</div>")

        ' Attachments that are not shown inside the text
        Dim rest = post.Attachments.Where(Function(a) Not inline.Contains(a.Aid)).ToList()
        If rest.Count > 0 Then
            sb.Append("<div class=""attachments""><div class=""label"">Anhänge</div>")
            For Each attachment As AttachmentInfo In rest
                AppendAttachment(sb, attachment, False)
            Next
            sb.Append("</div>")
        End If

        If post.EditTime > 0 AndAlso post.Editor.Length > 0 AndAlso post.EditTime - post.Dateline > 60 Then
            sb.Append("<div class=""foot"">Bearbeitet von ").Append(TextUtil.Html(post.Editor)).Append(", ").Append(Fmt.Relative(post.EditTime))
            If post.EditReason.Length > 0 Then sb.Append(" · Grund: ").Append(TextUtil.Html(post.EditReason))
            sb.Append("</div>")
        End If

        If showSignatures AndAlso post.Signature.Length > 0 Then
            sb.Append("<div class=""sig"">").Append(post.Signature).Append("</div>")
        End If

        ' Actions
        sb.Append("<div class=""actions"">")
        sb.Append("<a href=""fg://copylink/").Append(post.Pid).Append(""">Link kopieren</a>")
        If post.CanQuote Then sb.Append("<a href=""fg://quote/").Append(post.Pid).Append(""">Zitieren</a>")
        If post.CanEdit Then sb.Append("<a href=""fg://edit/").Append(post.Pid).Append(""">Bearbeiten</a>")
        If post.CanDelete Then
            sb.Append("<a class=""danger"" href=""fg://delete/").Append(post.Pid).Append(""">").Append(If(post.IsFirst, "Thema löschen", "Löschen")).Append("</a>")
        End If
        sb.Append("</div>")
        sb.Append("</div>")
    End Sub

    ''' <summary>Replaces [attachment=X] markers with images or download links.</summary>
    Private Shared Function ReplaceAttachments(html As String, attachments As List(Of AttachmentInfo), inline As HashSet(Of Integer)) As String
        Return AttachmentTag.Replace(If(html, ""),
            Function(m)
                Dim aid = Integer.Parse(m.Groups(1).Value)
                Dim attachment = If(attachments Is Nothing, Nothing, attachments.FirstOrDefault(Function(a) a.Aid = aid))
                If attachment Is Nothing Then
                    attachment = New AttachmentInfo With {.Aid = aid, .FileName = "Anhang " & aid, .IsImage = True, .HasThumbnail = False}
                End If
                inline.Add(aid)
                Dim sb As New StringBuilder()
                AppendAttachment(sb, attachment, True)
                Return sb.ToString()
            End Function)
    End Function

    Private Shared Sub AppendAttachment(sb As StringBuilder, attachment As AttachmentInfo, inline As Boolean)
        If attachment.IsImage Then
            sb.Append("<a class=""").Append(If(inline, "att-inline", "att-img")).Append(""" href=""fg://image/").Append(attachment.Aid)
            sb.Append(""" title=""").Append(TextUtil.Html(attachment.FileName)).Append(""">")
            sb.Append("<img src=""").Append(Placeholder).Append(""" data-aid=""").Append(attachment.Aid)
            sb.Append(""" data-thumb=""").Append(If(attachment.HasThumbnail AndAlso Not inline, "1", "0")).Append(""" alt="""">")
            sb.Append("</a>")
        Else
            sb.Append("<a class=""att-file"" href=""fg://download/").Append(attachment.Aid).Append(""">")
            sb.Append("&#128206; ").Append(TextUtil.Html(attachment.FileName))
            If attachment.FileSize > 0 Then sb.Append("<span>").Append(Fmt.FileSize(attachment.FileSize)).Append("</span>")
            sb.Append("</a>")
        End If
    End Sub

    ' ---------- Private message ----------

    Public Shared Function Message(pm As PmMessage) As String
        Dim sb As New StringBuilder()
        sb.Append("<div class=""post""><div class=""pm-head""><div class=""subject"">").Append(TextUtil.Html(pm.Subject)).Append("</div>")
        sb.Append("<div class=""meta"">Von <a href=""fg://user/").Append(pm.From.Uid).Append(""">").Append(pm.From.UsernameHtml).Append("</a>")
        If pm.Recipient IsNot Nothing AndAlso pm.Recipient.Username.Length > 0 Then
            sb.Append(" an ").Append(TextUtil.Html(pm.Recipient.Username))
        End If
        sb.Append(" · ").Append(Fmt.FullDate(pm.Dateline)).Append("</div></div>")
        sb.Append("<div class=""body"">").Append(pm.Html).Append("</div></div>")
        Return Document(sb.ToString())
    End Function

End Class
