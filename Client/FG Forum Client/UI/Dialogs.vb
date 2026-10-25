Imports System.Text

''' <summary>Base for the small dark dialogs.</summary>
Friend Class DarkDialog
    Inherits Form

    Public Sub New()
        FormBorderStyle = FormBorderStyle.FixedDialog
        StartPosition = FormStartPosition.CenterParent
        ShowInTaskbar = False
        MaximizeBox = False
        MinimizeBox = False
        BackColor = Theme.Back
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
        AutoScaleMode = AutoScaleMode.Dpi
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Theme.UseDarkTitleBar(Me)
    End Sub

    Protected Shared Function MakeLabel(text As String, Optional muted As Boolean = True) As Label
        Return New Label() With {.Text = text, .AutoSize = False, .ForeColor = If(muted, Theme.TextMuted, Theme.TextMain),
                                 .Font = If(muted, Theme.SmallFont, Theme.BodyFont), .BackColor = Color.Transparent}
    End Function
End Class


''' <summary>Asks for one line of text.</summary>
Friend Class InputDialog
    Inherits DarkDialog

    Private ReadOnly promptLabel As Label = MakeLabel("", False)
    Private ReadOnly inputBox As New DarkTextBox()
    Private ReadOnly okButton As New ModernButton() With {.Text = "OK", .Kind = ButtonKind.Primary}
    Private ReadOnly cancelButton As New ModernButton() With {.Text = "Abbrechen"}

    Public Sub New(title As String, prompt As String, defaultValue As String)
        Text = title
        ClientSize = New System.Drawing.Size(460, 170)
        promptLabel.Text = prompt
        inputBox.Text = defaultValue
        promptLabel.SetBounds(20, 16, 420, 44)
        inputBox.SetBounds(20, 66, 420, 34)
        cancelButton.SetBounds(220, 120, 104, 34)
        okButton.SetBounds(336, 120, 104, 34)
        Controls.AddRange(New Control() {promptLabel, inputBox, okButton, cancelButton})
        AddHandler okButton.Click, Sub(s, e) Close(DialogResult.OK)
        AddHandler cancelButton.Click, Sub(s, e) Close(DialogResult.Cancel)
        AddHandler inputBox.KeyDown,
            Sub(s, e)
                If e.KeyCode = Keys.Enter Then
                    e.SuppressKeyPress = True
                    Close(DialogResult.OK)
                ElseIf e.KeyCode = Keys.Escape Then
                    Close(DialogResult.Cancel)
                End If
            End Sub
    End Sub

    Private Overloads Sub Close(result As DialogResult)
        DialogResult = result
        MyBase.Close()
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        inputBox.Focus()
        inputBox.Inner.SelectAll()
    End Sub

    ''' <summary>Returns the text or Nothing if cancelled.</summary>
    Public Shared Function Ask(owner As IWin32Window, title As String, prompt As String, Optional defaultValue As String = "") As String
        Using dialog As New InputDialog(title, prompt, defaultValue)
            If dialog.ShowDialog(owner) = DialogResult.OK Then Return dialog.inputBox.Text
            Return Nothing
        End Using
    End Function
End Class


''' <summary>Shows a new API key once, with a copy button.</summary>
Friend Class SecretDialog
    Inherits DarkDialog

    Public Sub New(title As String, info As String, secret As String)
        Text = title
        ClientSize = New System.Drawing.Size(520, 200)
        Dim infoLabel = MakeLabel(info, False)
        infoLabel.SetBounds(20, 16, 480, 50)
        Dim box As New DarkTextBox() With {.Text = secret, .ReadOnly = True}
        box.Inner.Font = Theme.MonoFont
        box.SetBounds(20, 74, 480, 36)
        Dim copyButton As New ModernButton() With {.Text = "Kopieren", .Kind = ButtonKind.Primary, .IconGlyph = ChrW(&HE8C8)}
        copyButton.SetBounds(276, 140, 112, 34)
        Dim closeButton As New ModernButton() With {.Text = "Schließen"}
        closeButton.SetBounds(396, 140, 104, 34)
        AddHandler copyButton.Click,
            Sub(s, e)
                Try
                    Clipboard.SetText(secret)
                    copyButton.Text = "Kopiert ✓"
                Catch
                End Try
            End Sub
        AddHandler closeButton.Click, Sub(s, e) Close()
        Controls.AddRange(New Control() {infoLabel, box, copyButton, closeButton})
    End Sub
End Class


''' <summary>
''' Release assistant: asks for name, version, features and changes of a tool
''' and turns it into a nicely formatted forum post.
''' </summary>
Friend Class ReleaseDialog
    Inherits DarkDialog

    Private ReadOnly nameBox As New DarkTextBox() With {.Placeholder = "z. B. App Monitor"}
    Private ReadOnly versionBox As New DarkTextBox() With {.Placeholder = "z. B. 2.0"}
    Private ReadOnly descriptionBox As New EditorBox()
    Private ReadOnly featuresBox As New EditorBox()
    Private ReadOnly changesBox As New EditorBox()
    Private ReadOnly requirementsBox As New DarkTextBox() With {.Placeholder = "z. B. Windows 10/11, .NET Framework 4.8"}
    Private ReadOnly downloadBox As New DarkTextBox() With {.Placeholder = "Download-Link (leer lassen, wenn du die Datei anhängst)"}
    Private ReadOnly sourceBox As New DarkTextBox() With {.Placeholder = "GitHub-Link zum Quellcode (optional)"}
    Private ReadOnly filesList As New EntryListBox() With {.ItemHeight = 34, .EmptyText = "Dateien und Screenshots hier hineinziehen"}
    Private ReadOnly addFilesButton As New ModernButton() With {.Text = "Dateien …", .IconGlyph = Theme.IconAttach}
    Private ReadOnly okButton As New ModernButton() With {.Text = "In den Editor übernehmen", .Kind = ButtonKind.Primary}
    Private ReadOnly cancelButton As New ModernButton() With {.Text = "Abbrechen"}

    Public ReadOnly Property Files As New List(Of String)()
    Public Property ResultSubject As String = ""
    Public Property ResultMessage As String = ""

    Private ReadOnly authorName As String

    Public Sub New(username As String)
        authorName = username
        Text = "Tool veröffentlichen"
        ClientSize = New System.Drawing.Size(760, 640)

        Dim y = 16
        AddRow("Name des Tools", nameBox, 20, y, 470)
        AddRow("Version", versionBox, 506, y, 234)
        y += 64
        AddRow("Kurzbeschreibung", WrapEditor(descriptionBox), 20, y, 720, 70)
        y += 100
        AddRow("Funktionen (eine pro Zeile)", WrapEditor(featuresBox), 20, y, 354, 110)
        AddRow("Änderungen in dieser Version (eine pro Zeile)", WrapEditor(changesBox), 386, y, 354, 110)
        y += 140
        AddRow("Voraussetzungen", requirementsBox, 20, y, 720)
        y += 64
        AddRow("Download", downloadBox, 20, y, 354)
        AddRow("Quellcode", sourceBox, 386, y, 354)
        y += 64
        Dim filesLabel = MakeLabel("Dateien und Screenshots (werden beim Absenden hochgeladen)")
        filesLabel.SetBounds(20, y, 600, 18)
        filesList.SetBounds(20, y + 22, 600, 76)
        filesList.AllowDrop = True
        addFilesButton.SetBounds(632, y + 22, 108, 34)
        Controls.AddRange(New Control() {filesLabel, filesList, addFilesButton})

        cancelButton.SetBounds(442, 590, 104, 34)
        okButton.SetBounds(556, 590, 184, 34)
        Controls.AddRange(New Control() {okButton, cancelButton})
        sourceBox.Text = AppInfo.GitHubUrl & "/"

        AddHandler addFilesButton.Click, AddressOf AddFiles_Click
        AddHandler filesList.DragEnter, Sub(s, e) e.Effect = If(e.Data.GetDataPresent(DataFormats.FileDrop), DragDropEffects.Copy, DragDropEffects.None)
        AddHandler filesList.DragDrop,
            Sub(s, e)
                Dim dropped = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
                If dropped IsNot Nothing Then AddFileList(dropped)
            End Sub
        AddHandler okButton.Click, AddressOf Ok_Click
        AddHandler cancelButton.Click,
            Sub(s, e)
                DialogResult = DialogResult.Cancel
                Close()
            End Sub
    End Sub

    Private Function WrapEditor(box As EditorBox) As Control
        Dim frame As New Panel() With {.BackColor = Theme.Input, .Padding = New Padding(8, 6, 4, 6)}
        box.Dock = DockStyle.Fill
        box.Font = Theme.BodyFont
        frame.Controls.Add(box)
        Return frame
    End Function

    Private Sub AddRow(caption As String, field As Control, x As Integer, y As Integer, fieldWidth As Integer, Optional fieldHeight As Integer = 34)
        Dim captionLabel = MakeLabel(caption)
        captionLabel.SetBounds(x, y, fieldWidth, 18)
        field.SetBounds(x, y + 22, fieldWidth, fieldHeight)
        Controls.Add(captionLabel)
        Controls.Add(field)
    End Sub

    Private Sub AddFiles_Click(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog() With {.Multiselect = True, .Title = "Dateien für den Beitrag auswählen"}
            If dialog.ShowDialog(Me) = DialogResult.OK Then AddFileList(dialog.FileNames)
        End Using
    End Sub

    Private Sub AddFileList(paths As IEnumerable(Of String))
        For Each filePath As String In paths
            If Not File.Exists(filePath) OrElse Files.Contains(filePath) Then Continue For
            Files.Add(filePath)
            filesList.Items.Add(New ListEntry With {
                .Title = Path.GetFileName(filePath),
                .Detail = Fmt.FileSize(New FileInfo(filePath).Length),
                .Tag = filePath})
        Next
    End Sub

    Private Sub Ok_Click(sender As Object, e As EventArgs)
        If nameBox.Text.Trim().Length = 0 Then
            MessageBox.Show(Me, "Bitte gib den Namen des Tools ein.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
            nameBox.Focus()
            Return
        End If
        Dim toolName = nameBox.Text.Trim()
        Dim version = versionBox.Text.Trim()
        ResultSubject = toolName & If(version.Length > 0, " v" & version, "")

        Dim sb As New StringBuilder()
        sb.AppendLine("[align=center][size=x-large][b]" & toolName & "[/b][/size]")
        sb.AppendLine("[size=small]" & If(version.Length > 0, "Version " & version & " · ", "") & "von " & authorName & "[/size][/align]")
        sb.AppendLine("[hr]")
        If descriptionBox.Text.Trim().Length > 0 Then
            sb.AppendLine("[b]Beschreibung[/b]")
            sb.AppendLine(descriptionBox.Text.Trim())
            sb.AppendLine()
        End If
        AppendList(sb, "Funktionen", featuresBox.Text)
        AppendList(sb, If(version.Length > 0, "Neu in Version " & version, "Änderungen"), changesBox.Text)
        If requirementsBox.Text.Trim().Length > 0 Then
            sb.AppendLine("[b]Voraussetzungen[/b]")
            sb.AppendLine(requirementsBox.Text.Trim())
            sb.AppendLine()
        End If
        sb.AppendLine("[b]Download[/b]")
        If downloadBox.Text.Trim().Length > 0 Then
            sb.AppendLine("[url=" & downloadBox.Text.Trim() & "]" & toolName & " herunterladen[/url]")
        Else
            sb.AppendLine("Siehe Anhang.")
        End If
        Dim source = sourceBox.Text.Trim()
        If source.Length > 0 AndAlso source.TrimEnd("/"c) <> AppInfo.GitHubUrl Then
            sb.AppendLine()
            sb.AppendLine("[b]Quellcode[/b]")
            sb.AppendLine("[url=" & source & "]" & source & "[/url]")
        End If
        ResultMessage = sb.ToString().TrimEnd() & Environment.NewLine

        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Shared Sub AppendList(sb As StringBuilder, caption As String, lines As String)
        Dim items = lines.Split(New String() {Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(l) l.Trim().TrimStart("-"c, "*"c, "•"c).Trim()).Where(Function(l) l.Length > 0).ToList()
        If items.Count = 0 Then Return
        sb.AppendLine("[b]" & caption & "[/b]")
        sb.AppendLine("[list]")
        For Each item As String In items
            sb.AppendLine("[*]" & item)
        Next
        sb.AppendLine("[/list]")
        sb.AppendLine()
    End Sub
End Class


''' <summary>Short profile of a member with a "send message" button.</summary>
Friend Class UserDialog
    Inherits DarkDialog

    Public Sub New(main As MainForm, data As Dictionary(Of String, Object))
        Dim user = UserBrief.From(data)
        Text = "Profil von " & user.Username
        ClientSize = New System.Drawing.Size(440, 300)

        Dim nameLabel = MakeLabel(user.Username, False)
        nameLabel.Font = Theme.TitleFont
        nameLabel.SetBounds(20, 16, 400, 36)
        Dim titleLabel = MakeLabel(TextUtil.StripTags(user.Usertitle))
        titleLabel.SetBounds(22, 54, 400, 18)

        Dim lines As New List(Of String) From {
            "Beiträge: " & Fmt.Number(user.Postnum),
            "Themen: " & Fmt.Number(Json.GetInt(data, "threadnum")),
            "Bewertung: " & Json.GetInt(data, "reputation"),
            "Mitglied seit: " & Fmt.FullDate(Json.GetLong(data, "regdate"))}
        Dim lastActive = Json.GetLong(data, "lastactive")
        lines.Add("Zuletzt online: " & If(lastActive > 0, Fmt.Relative(lastActive), "versteckt"))
        Dim website = Json.GetString(data, "website")
        If website.Length > 0 Then lines.Add("Webseite: " & website)

        Dim infoLabel = MakeLabel(String.Join(Environment.NewLine, lines), False)
        infoLabel.SetBounds(22, 90, 400, 140)

        Dim messageButton As New ModernButton() With {.Text = "Nachricht senden", .Kind = ButtonKind.Primary, .IconGlyph = Theme.IconMail}
        messageButton.SetBounds(150, 248, 160, 34)
        messageButton.Enabled = main.CurrentUser IsNot Nothing AndAlso main.CurrentUser.CanSendPms AndAlso user.Uid <> main.CurrentUser.User.Uid
        Dim closeButton As New ModernButton() With {.Text = "Schließen"}
        closeButton.SetBounds(318, 248, 104, 34)

        AddHandler messageButton.Click,
            Sub(s, e)
                Close()
                main.OpenNewMessage(user.Username)
            End Sub
        AddHandler closeButton.Click, Sub(s, e) Close()
        If website.Length > 0 Then
            Dim webButton As New ModernButton() With {.Text = "Webseite", .IconGlyph = Theme.IconOpen}
            webButton.SetBounds(20, 248, 120, 34)
            AddHandler webButton.Click, Sub(s, e) AppInfo.OpenUrl(If(website.StartsWith("http", StringComparison.OrdinalIgnoreCase), website, "https://" & website))
            Controls.Add(webButton)
        End If
        Controls.AddRange(New Control() {nameLabel, titleLabel, infoLabel, messageButton, closeButton})
    End Sub
End Class
