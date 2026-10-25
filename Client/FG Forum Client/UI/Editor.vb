Imports System.Drawing.Imaging

''' <summary>Files that were dropped or pasted into the editor.</summary>
Friend Class FilesEventArgs
    Inherits EventArgs

    Public ReadOnly Property Files As List(Of String)

    Public Sub New(fileList As List(Of String))
        Files = fileList
    End Sub
End Class


''' <summary>
''' Multi-line text box for MyCode. Pasting an image (Strg+V, screenshot) or dropping files
''' raises <see cref="FilesAdded"/>, so the page can upload them right away.
''' </summary>
Friend Class EditorBox
    Inherits TextBox

    Private Const WM_PASTE As Integer = &H302

    Public Event FilesAdded As EventHandler(Of FilesEventArgs)

    Public Sub New()
        Multiline = True
        ScrollBars = ScrollBars.Vertical
        AcceptsReturn = True
        AcceptsTab = True
        WordWrap = True
        MaxLength = 0
        BorderStyle = BorderStyle.None
        BackColor = Theme.Input
        ForeColor = Theme.TextMain
        Font = Theme.PickFont(10.5F, FontStyle.Regular, "Segoe UI")
        AllowDrop = True
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Theme.UseDarkScrollbars(Me)
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = WM_PASTE Then
            Try
                If Clipboard.ContainsFileDropList() Then
                    Dim files As New List(Of String)()
                    For Each item As String In Clipboard.GetFileDropList()
                        If File.Exists(item) Then files.Add(item)
                    Next
                    If files.Count > 0 Then
                        RaiseFiles(files)
                        Return
                    End If
                ElseIf Clipboard.ContainsImage() Then
                    Dim saved = SaveClipboardImage()
                    If saved.Length > 0 Then
                        RaiseFiles(New List(Of String) From {saved})
                        Return
                    End If
                End If
            Catch
                ' Fall back to normal paste
            End Try
        End If
        MyBase.WndProc(m)
    End Sub

    ''' <summary>Saves a screenshot from the clipboard as PNG in the temp folder.</summary>
    Private Shared Function SaveClipboardImage() As String
        Using image = Clipboard.GetImage()
            If image Is Nothing Then Return ""
            Dim folder = Path.Combine(Path.GetTempPath(), "FG Forum Client")
            Directory.CreateDirectory(folder)
            Dim target = Path.Combine(folder, "Bild_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".png")
            image.Save(target, ImageFormat.Png)
            Return target
        End Using
    End Function

    Protected Overrides Sub OnDragEnter(e As DragEventArgs)
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.Copy
        Else
            e.Effect = DragDropEffects.None
        End If
        MyBase.OnDragEnter(e)
    End Sub

    Protected Overrides Sub OnDragDrop(e As DragEventArgs)
        Dim dropped = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
        If dropped IsNot Nothing Then
            RaiseFiles(dropped.Where(Function(f) File.Exists(f)).ToList())
        End If
        MyBase.OnDragDrop(e)
    End Sub

    Private Sub RaiseFiles(files As List(Of String))
        If files.Count = 0 Then Return
        RaiseEvent FilesAdded(Me, New FilesEventArgs(files))
    End Sub

    ' ---------- MyCode helpers ----------

    ''' <summary>Puts tags around the selection (or inserts them with the cursor in between).</summary>
    Public Sub WrapSelection(openTag As String, closeTag As String)
        Dim start = SelectionStart
        Dim selected = SelectedText
        SelectedText = openTag & selected & closeTag
        If selected.Length = 0 Then
            SelectionStart = start + openTag.Length
            SelectionLength = 0
        Else
            SelectionStart = start
            SelectionLength = openTag.Length + selected.Length + closeTag.Length
        End If
        Focus()
    End Sub

    Public Sub InsertText(value As String)
        SelectedText = value
        Focus()
    End Sub

    ''' <summary>Ctrl+B / Ctrl+I / Ctrl+U like in the browser editor.</summary>
    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.Control AndAlso Not e.Alt Then
            Select Case e.KeyCode
                Case Keys.B
                    WrapSelection("[b]", "[/b]")
                    e.SuppressKeyPress = True
                Case Keys.I
                    WrapSelection("[i]", "[/i]")
                    e.SuppressKeyPress = True
                Case Keys.U
                    WrapSelection("[u]", "[/u]")
                    e.SuppressKeyPress = True
                Case Keys.A
                    SelectAll()
                    e.SuppressKeyPress = True
            End Select
        End If
        MyBase.OnKeyDown(e)
    End Sub

End Class


''' <summary>Editor with a MyCode toolbar and a rounded frame.</summary>
Friend Class MyCodeEditor
    Inherits Control

    Public ReadOnly Box As New EditorBox()
    Private ReadOnly toolbar As New FlowLayoutPanel()
    Private ReadOnly tips As New ToolTip()

    ''' <summary>"Datei anhängen" in the toolbar was clicked.</summary>
    Public Event AttachClicked As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Input
        toolbar.BackColor = Theme.Input
        toolbar.WrapContents = False
        toolbar.Padding = New Padding(4, 4, 4, 0)
        Controls.Add(toolbar)
        Controls.Add(Box)

        AddTool("B", "Fett (Strg+B)", Sub() Box.WrapSelection("[b]", "[/b]"))
        AddTool("I", "Kursiv (Strg+I)", Sub() Box.WrapSelection("[i]", "[/i]"))
        AddTool("U", "Unterstrichen (Strg+U)", Sub() Box.WrapSelection("[u]", "[/u]"))
        AddTool("S", "Durchgestrichen", Sub() Box.WrapSelection("[s]", "[/s]"))
        AddSeparator()
        AddTool(ChrW(&HE8E9), "Schriftgröße", AddressOf ChooseSize, True)
        AddTool(ChrW(&HE790), "Schriftfarbe", AddressOf ChooseColor, True)
        AddTool(ChrW(&HE8E3), "Zentrieren", Sub() Box.WrapSelection("[align=center]", "[/align]"), True)
        AddSeparator()
        AddTool(ChrW(&HE71B), "Link einfügen", AddressOf InsertLink, True)
        AddTool(ChrW(&HEB9F), "Bild über Adresse einfügen", AddressOf InsertImageUrl, True)
        AddTool(ChrW(&HE714), "Video (YouTube) einfügen", AddressOf InsertVideo, True)
        AddSeparator()
        AddTool(ChrW(&HE8BD), "Zitat", Sub() Box.WrapSelection("[quote]", "[/quote]"), True)
        AddTool(ChrW(&HE943), "Code", Sub() Box.WrapSelection("[code]", "[/code]"), True)
        AddTool(ChrW(&HE8FD), "Aufzählung", AddressOf InsertList, True)
        AddTool(ChrW(&HE738), "Trennlinie", Sub() Box.InsertText(Environment.NewLine & "[hr]" & Environment.NewLine), True)
        AddSeparator()
        AddTool(Theme.IconAttach, "Datei oder Bild anhängen (oder einfach hineinziehen / Strg+V)", Sub() RaiseEvent AttachClicked(Me, EventArgs.Empty), True)

        Size = New System.Drawing.Size(400, 240)
    End Sub

    Public Overrides Property Text As String
        Get
            Return If(Box Is Nothing, "", Box.Text)
        End Get
        Set(value As String)
            If Box IsNot Nothing Then Box.Text = value
        End Set
    End Property

    Private Sub AddTool(label As String, tipText As String, action As Action, Optional isGlyph As Boolean = False)
        Dim button As New ModernButton() With {
            .Kind = ButtonKind.Ghost,
            .Size = New System.Drawing.Size(30, 28),
            .Margin = New Padding(0, 0, 2, 0),
            .BackColor = Theme.Input}
        If isGlyph Then
            button.IconGlyph = label
        Else
            button.Text = label
            button.Font = New Font(Theme.BodyBoldFont.FontFamily, 10.0F, If(label = "B", FontStyle.Bold, If(label = "I", FontStyle.Italic, If(label = "U", FontStyle.Underline, FontStyle.Strikeout))))
        End If
        tips.SetToolTip(button, tipText)
        AddHandler button.Click, Sub(s, e) action()
        toolbar.Controls.Add(button)
    End Sub

    Private Sub AddSeparator()
        toolbar.Controls.Add(New Panel() With {.Size = New System.Drawing.Size(1, 20), .BackColor = Theme.Border, .Margin = New Padding(4, 4, 6, 0)})
    End Sub

    Private Sub ChooseSize()
        Dim menu As New ContextMenuStrip()
        For Each pair As String() In New String()() {New String() {"Sehr klein", "xx-small"}, New String() {"Klein", "small"}, New String() {"Groß", "large"}, New String() {"Sehr groß", "x-large"}, New String() {"Riesig", "xx-large"}}
            Dim sizeName = pair(1)
            menu.Items.Add(pair(0), Nothing, Sub(s, e) Box.WrapSelection("[size=" & sizeName & "]", "[/size]"))
        Next
        menu.Show(System.Windows.Forms.Cursor.Position)
    End Sub

    Private Sub ChooseColor()
        Using dialog As New ColorDialog() With {.FullOpen = True}
            If dialog.ShowDialog(FindForm()) = DialogResult.OK Then
                Dim c = dialog.Color
                Box.WrapSelection("[color=#" & c.R.ToString("x2") & c.G.ToString("x2") & c.B.ToString("x2") & "]", "[/color]")
            End If
        End Using
    End Sub

    Private Sub InsertLink()
        Dim url = InputDialog.Ask(FindForm(), "Link einfügen", "Adresse (https://…)", "https://")
        If String.IsNullOrWhiteSpace(url) OrElse url = "https://" Then Return
        If Box.SelectionLength > 0 Then
            Box.WrapSelection("[url=" & url.Trim() & "]", "[/url]")
        Else
            Box.InsertText("[url]" & url.Trim() & "[/url]")
        End If
    End Sub

    Private Sub InsertImageUrl()
        Dim url = InputDialog.Ask(FindForm(), "Bild einfügen", "Adresse des Bildes (https://…/bild.png)" & Environment.NewLine & "Eigene Bilder kannst du einfach hineinziehen oder mit Strg+V einfügen.", "https://")
        If String.IsNullOrWhiteSpace(url) OrElse url = "https://" Then Return
        Box.InsertText("[img]" & url.Trim() & "[/img]")
    End Sub

    Private Sub InsertVideo()
        Dim url = InputDialog.Ask(FindForm(), "Video einfügen", "YouTube-Adresse des Videos", "https://www.youtube.com/watch?v=")
        If String.IsNullOrWhiteSpace(url) OrElse url.EndsWith("v=") Then Return
        Box.InsertText("[video=youtube]" & url.Trim() & "[/video]")
    End Sub

    Private Sub InsertList()
        Dim lines = Box.SelectedText.Split(New String() {Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)
        Dim sb As New System.Text.StringBuilder("[list]" & Environment.NewLine)
        If lines.Length = 0 Then
            sb.Append("[*]" & Environment.NewLine)
        Else
            For Each line As String In lines
                sb.Append("[*]").Append(line.Trim()).Append(Environment.NewLine)
            Next
        End If
        sb.Append("[/list]")
        Box.InsertText(sb.ToString())
    End Sub

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        MyBase.OnLayout(e)
        If toolbar Is Nothing OrElse Box Is Nothing Then Return
        toolbar.SetBounds(6, 4, Width - 12, 34)
        Box.SetBounds(12, 42, Width - 20, Height - 50)
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        e.Graphics.Clear(If(Parent IsNot Nothing, Parent.BackColor, Theme.Back))
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim border = If(Box.Focused, Theme.Accent, Theme.Border)
        Theme.FillRounded(e.Graphics, New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F), 8, Theme.Input, border)
        Using line As New Pen(Theme.Border)
            e.Graphics.DrawLine(line, 8, 39, Width - 8, 39)
        End Using
    End Sub

    Protected Overrides Sub OnCreateControl()
        MyBase.OnCreateControl()
        AddHandler Box.GotFocus, Sub(s, e) Invalidate()
        AddHandler Box.LostFocus, Sub(s, e) Invalidate()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
