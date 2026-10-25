Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices

' =====================================================================
'  Custom dark controls. All of them paint themselves, so the app looks
'  the same on every Windows version.
' =====================================================================

''' <summary>Base class for controls that paint everything themselves.</summary>
Friend MustInherit Class PaintedControl
    Inherits Control

    Protected hovering As Boolean

    Protected Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or
                 ControlStyles.SupportsTransparentBackColor, True)
        BackColor = Theme.Surface
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        hovering = True
        Invalidate()
        MyBase.OnMouseEnter(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        hovering = False
        Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        Invalidate()
        MyBase.OnEnabledChanged(e)
    End Sub

    Protected Overrides Sub OnTextChanged(e As EventArgs)
        Invalidate()
        MyBase.OnTextChanged(e)
    End Sub

    ''' <summary>Fills the area around rounded shapes with the parent's color.</summary>
    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        Dim back = If(Parent IsNot Nothing, Parent.BackColor, Theme.Back)
        e.Graphics.Clear(back)
    End Sub

End Class


Friend Enum ButtonKind
    Secondary
    Primary
    Danger
    Ghost
End Enum

''' <summary>Flat rounded button with optional icon glyph.</summary>
Friend Class ModernButton
    Inherits PaintedControl

    Private _kind As ButtonKind = ButtonKind.Secondary
    Private _icon As String = ""
    Private pressed As Boolean

    Public Sub New()
        SetStyle(ControlStyles.StandardClick, True)
        Cursor = Cursors.Hand
        Font = Theme.BodyBoldFont
        Size = New System.Drawing.Size(120, 34)
    End Sub

    Public Property Kind As ButtonKind
        Get
            Return _kind
        End Get
        Set(value As ButtonKind)
            _kind = value
            Invalidate()
        End Set
    End Property

    ''' <summary>Glyph from <see cref="Theme"/>, e.g. Theme.IconPlay.</summary>
    Public Property IconGlyph As String
        Get
            Return _icon
        End Get
        Set(value As String)
            _icon = If(value, "")
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        pressed = (e.Button = MouseButtons.Left)
        Invalidate()
        MyBase.OnMouseDown(e)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        pressed = False
        Invalidate()
        MyBase.OnMouseUp(e)
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        Cursor = If(Enabled, Cursors.Hand, Cursors.Default)
        MyBase.OnEnabledChanged(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        Dim fill As Color
        Dim border As Color
        Dim textColor As Color

        If Not Enabled Then
            fill = Theme.Surface
            border = Theme.Border
            textColor = Theme.TextDim
        Else
            Select Case _kind
                Case ButtonKind.Primary
                    fill = If(pressed, Theme.AccentPressed, If(hovering, Theme.AccentHover, Theme.Accent))
                    border = fill
                    textColor = Theme.OnAccent
                Case ButtonKind.Danger
                    fill = If(pressed, Theme.Blend(Theme.Danger, Color.Black, 0.2), If(hovering, Theme.Blend(Theme.Danger, Color.White, 0.12), Theme.Danger))
                    border = fill
                    textColor = Color.White
                Case ButtonKind.Ghost
                    fill = If(pressed, Theme.Border, If(hovering, Theme.SurfaceHover, If(Parent IsNot Nothing, Parent.BackColor, Theme.Back)))
                    border = fill
                    textColor = Theme.TextMain
                Case Else
                    fill = If(pressed, Theme.Input, If(hovering, Theme.SurfaceHover, Theme.Surface))
                    border = If(hovering, Theme.TextDim, Theme.Border)
                    textColor = Theme.TextMain
            End Select
        End If

        Theme.FillRounded(g, New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F), 6, fill, border)

        Dim flags = TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis
        If _icon.Length = 0 Then
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, textColor, flags Or TextFormatFlags.HorizontalCenter)
            Return
        End If

        Dim iconSize = TextRenderer.MeasureText(g, _icon, Theme.IconFontSmall, System.Drawing.Size.Empty, TextFormatFlags.NoPadding)
        Dim textSize = TextRenderer.MeasureText(g, Text, Font, System.Drawing.Size.Empty, TextFormatFlags.NoPadding)
        Dim gap = If(Text.Length > 0, 8, 0)
        Dim total = iconSize.Width + gap + textSize.Width
        Dim x = Math.Max(8, (Width - total) \ 2)
        TextRenderer.DrawText(g, _icon, Theme.IconFontSmall, New Rectangle(x, 0, iconSize.Width + 2, Height), textColor, flags)
        If Text.Length > 0 Then
            TextRenderer.DrawText(g, Text, Font, New Rectangle(x + iconSize.Width + gap, 0, Width - x - iconSize.Width - gap - 6, Height), textColor, flags)
        End If
    End Sub

End Class


''' <summary>Entry of the left navigation bar.</summary>
Friend Class NavButton
    Inherits PaintedControl

    Private _selected As Boolean
    Private _icon As String = ""

    Public Sub New()
        SetStyle(ControlStyles.StandardClick, True)
        BackColor = Theme.Sidebar
        Cursor = Cursors.Hand
        Size = New System.Drawing.Size(190, 40)
    End Sub

    Public Property IconGlyph As String
        Get
            Return _icon
        End Get
        Set(value As String)
            _icon = If(value, "")
            Invalidate()
        End Set
    End Property

    Public Property Selected As Boolean
        Get
            Return _selected
        End Get
        Set(value As Boolean)
            _selected = value
            Invalidate()
        End Set
    End Property

    Private _badgeText As String = ""

    ''' <summary>Small counter on the right (e.g. unread messages). Empty = hidden.</summary>
    Public Property BadgeText As String
        Get
            Return _badgeText
        End Get
        Set(value As String)
            _badgeText = If(value, "")
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        If _selected OrElse hovering Then
            Theme.FillRounded(g, New RectangleF(0, 0, Width - 1, Height - 1), 6, If(_selected, Theme.Surface, Theme.Blend(Theme.Sidebar, Theme.Surface, 0.6)))
        End If
        If _selected Then
            Theme.FillRounded(g, New RectangleF(0, 10, 3, Height - 20), 1.5F, Theme.Accent)
        End If

        Dim labelColor = If(_selected, Theme.TextMain, Theme.TextMuted)
        TextRenderer.DrawText(g, _icon, Theme.IconFont, New Rectangle(16, 0, 24, Height),
                              If(_selected, Theme.Accent, labelColor), TextFormatFlags.VerticalCenter Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.NoPadding)
        TextRenderer.DrawText(g, Text, If(_selected, Theme.BodyBoldFont, Theme.BodyFont), New Rectangle(50, 0, Width - 56, Height),
                              labelColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        If _badgeText.Length > 0 Then
            Dim badgeSize = TextRenderer.MeasureText(g, _badgeText, Theme.SmallFont, System.Drawing.Size.Empty, TextFormatFlags.NoPadding)
            Dim badgeWidth = Math.Max(20, badgeSize.Width + 12)
            Dim badgeRect As New Rectangle(Width - badgeWidth - 10, (Height - 18) \ 2, badgeWidth, 18)
            Theme.FillRounded(g, badgeRect, 9, Theme.Accent)
            TextRenderer.DrawText(g, _badgeText, Theme.SmallFont, badgeRect, Theme.OnAccent,
                                  TextFormatFlags.VerticalCenter Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.NoPadding)
        End If
    End Sub

End Class


''' <summary>Rounded container with an optional title. Children sit on the surface color.</summary>
Friend Class CardPanel
    Inherits Panel

    Private _title As String = ""
    Private _subtitle As String = ""

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Surface
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
    End Sub

    Public Property Title As String
        Get
            Return _title
        End Get
        Set(value As String)
            Dim before = ContentTop
            _title = If(value, "")
            Invalidate()
            If ContentTop <> before AndAlso Parent IsNot Nothing Then Parent.PerformLayout()
        End Set
    End Property

    Public Property Subtitle As String
        Get
            Return _subtitle
        End Get
        Set(value As String)
            Dim before = ContentTop
            _subtitle = If(value, "")
            Invalidate()
            If ContentTop <> before AndAlso Parent IsNot Nothing Then Parent.PerformLayout()
        End Set
    End Property

    ''' <summary>Y position where content starts below the title.</summary>
    Public ReadOnly Property ContentTop As Integer
        Get
            If _title.Length = 0 Then Return 16
            Return If(_subtitle.Length = 0, 46, 62)
        End Get
    End Property

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(If(Parent IsNot Nothing, Parent.BackColor, Theme.Back))
        Theme.FillRounded(g, New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F), 10, Theme.Surface, Theme.Border)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If _title.Length = 0 Then Return
        TextRenderer.DrawText(e.Graphics, _title, Theme.HeadingFont, New Point(16, 14), Theme.TextMain, TextFormatFlags.NoPadding)
        If _subtitle.Length > 0 Then
            TextRenderer.DrawText(e.Graphics, _subtitle, Theme.SmallFont, New Rectangle(16, 38, Width - 32, 18), Theme.TextMuted,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        End If
    End Sub

End Class


''' <summary>On/off switch with a label on the right.</summary>
Friend Class ToggleSwitch
    Inherits PaintedControl

    Private _checked As Boolean

    Public Event CheckedChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.StandardClick, True)
        Cursor = Cursors.Hand
        Size = New System.Drawing.Size(220, 24)
    End Sub

    <DefaultValue(False)>
    Public Property Checked As Boolean
        Get
            Return _checked
        End Get
        Set(value As Boolean)
            If _checked = value Then Return
            _checked = value
            Invalidate()
            RaiseEvent CheckedChanged(Me, EventArgs.Empty)
        End Set
    End Property

    Protected Overrides Sub OnClick(e As EventArgs)
        If Enabled Then Checked = Not Checked
        MyBase.OnClick(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        Dim trackRect As New RectangleF(0.5F, (Height - 20) / 2.0F, 38, 20)
        Dim trackColor = If(Not Enabled, Theme.Border, If(_checked, Theme.Accent, If(hovering, Theme.TextDim, Theme.Border)))
        Theme.FillRounded(g, trackRect, 10, trackColor)

        Dim knobX = If(_checked, trackRect.Right - 17, trackRect.X + 3)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using knob As New SolidBrush(If(Enabled, Color.White, Theme.TextMuted))
            g.FillEllipse(knob, knobX, trackRect.Y + 3, 14, 14)
        End Using

        If Text.Length > 0 Then
            TextRenderer.DrawText(g, Text, Font, New Rectangle(48, 0, Width - 48, Height),
                                  If(Enabled, Theme.TextMain, Theme.TextDim),
                                  TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        End If
    End Sub

End Class


''' <summary>Single-line text input with a rounded dark border.</summary>
Friend Class DarkTextBox
    Inherits Control

    Private Const EM_SETCUEBANNER As Integer = &H1501
    Private ReadOnly box As TextBox
    Private _placeholder As String = ""

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As String) As IntPtr
    End Function

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Input
        Cursor = Cursors.IBeam

        box = New TextBox() With {
            .BorderStyle = BorderStyle.None,
            .BackColor = Theme.Input,
            .ForeColor = Theme.TextMain,
            .Font = Theme.BodyFont
        }
        AddHandler box.TextChanged, Sub(s, e) OnTextChanged(e)
        AddHandler box.KeyDown, Sub(s, e) OnKeyDown(e)
        AddHandler box.KeyPress, Sub(s, e) OnKeyPress(e)
        AddHandler box.GotFocus, Sub(s, e) Invalidate()
        AddHandler box.LostFocus,
            Sub(s, e)
                Invalidate()
                OnLeave(e)
            End Sub
        AddHandler box.HandleCreated, Sub(s, e) ApplyPlaceholder()
        Controls.Add(box)
        Size = New System.Drawing.Size(220, 34)
    End Sub

    ''' <summary>The inner TextBox, for anything not wrapped here.</summary>
    Public ReadOnly Property Inner As TextBox
        Get
            Return box
        End Get
    End Property

    Public Overrides Property Text As String
        Get
            Return If(box Is Nothing, "", box.Text)
        End Get
        Set(value As String)
            If box IsNot Nothing Then box.Text = value
        End Set
    End Property

    Public Property Placeholder As String
        Get
            Return _placeholder
        End Get
        Set(value As String)
            _placeholder = If(value, "")
            ApplyPlaceholder()
        End Set
    End Property

    Public Property UsePassword As Boolean
        Get
            Return box.UseSystemPasswordChar
        End Get
        Set(value As Boolean)
            box.UseSystemPasswordChar = value
        End Set
    End Property

    Public Property [ReadOnly] As Boolean
        Get
            Return box.ReadOnly
        End Get
        Set(value As Boolean)
            box.ReadOnly = value
            box.BackColor = Theme.Input
        End Set
    End Property

    Private Sub ApplyPlaceholder()
        If box.IsHandleCreated Then SendMessage(box.Handle, EM_SETCUEBANNER, New IntPtr(1), _placeholder)
    End Sub

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        MyBase.OnLayout(e)
        If box Is Nothing Then Return
        box.SetBounds(10, (Height - box.PreferredHeight) \ 2 + 1, Math.Max(10, Width - 20), box.PreferredHeight)
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        box.Focus()
        MyBase.OnMouseDown(e)
    End Sub

    Protected Overrides Sub OnGotFocus(e As EventArgs)
        box.Focus()
        MyBase.OnGotFocus(e)
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        box.Enabled = Enabled
        Invalidate()
        MyBase.OnEnabledChanged(e)
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        e.Graphics.Clear(If(Parent IsNot Nothing, Parent.BackColor, Theme.Back))
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim border = If(box.Focused, Theme.Accent, Theme.Border)
        Theme.FillRounded(e.Graphics, New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F), 6, Theme.Input, border)
    End Sub

End Class


''' <summary>Text box that only accepts whole numbers within a range.</summary>
Friend Class NumberBox
    Inherits DarkTextBox

    Public Property Minimum As Long = 0
    Public Property Maximum As Long = Integer.MaxValue

    Public Property Value As Long
        Get
            Dim v As Long
            If Long.TryParse(Text, v) Then Return Math.Min(Maximum, Math.Max(Minimum, v))
            Return Minimum
        End Get
        Set(v As Long)
            Text = Math.Min(Maximum, Math.Max(Minimum, v)).ToString()
        End Set
    End Property

    Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)
        If Char.IsControl(e.KeyChar) OrElse Char.IsDigit(e.KeyChar) Then
            ' allowed
        ElseIf e.KeyChar = "-"c AndAlso Minimum < 0 AndAlso Inner.SelectionStart = 0 Then
            ' allowed
        Else
            e.Handled = True
        End If
        MyBase.OnKeyPress(e)
    End Sub

    Protected Overrides Sub OnLeave(e As EventArgs)
        If Text.Length > 0 Then Value = Value
        MyBase.OnLeave(e)
    End Sub

End Class


''' <summary>Drop-down list in the dark theme.</summary>
Friend Class DarkComboBox
    Inherits ComboBox

    Private Const WM_PAINT As Integer = &HF
    Private hovering As Boolean

    Public Sub New()
        DrawMode = DrawMode.OwnerDrawFixed
        DropDownStyle = ComboBoxStyle.DropDownList
        FlatStyle = FlatStyle.Flat
        BackColor = Theme.Input
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
        ItemHeight = 26
        Cursor = Cursors.Hand
        Width = 220
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Theme.UseDarkScrollbars(Me)
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        hovering = True
        Invalidate()
        MyBase.OnMouseEnter(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        hovering = False
        Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    ''' <summary>Scroll the surrounding page instead of changing the selection by accident.</summary>
    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        If Not DroppedDown Then
            Dim host = Parent
            While host IsNot Nothing AndAlso Not TypeOf host Is ScrollPanel
                host = host.Parent
            End While
            If host IsNot Nothing Then
                DirectCast(host, ScrollPanel).ScrollBy(e.Delta)
                Dim handledArgs = TryCast(e, HandledMouseEventArgs)
                If handledArgs IsNot Nothing Then handledArgs.Handled = True
                Return
            End If
        End If
        MyBase.OnMouseWheel(e)
    End Sub

    Protected Overrides Sub OnDrawItem(e As DrawItemEventArgs)
        If e.Index < 0 Then Return
        Dim isSelected = (e.State And DrawItemState.Selected) = DrawItemState.Selected
        Using b As New SolidBrush(If(isSelected, Theme.AccentSoft, Theme.Input))
            e.Graphics.FillRectangle(b, e.Bounds)
        End Using
        Dim r = e.Bounds
        r.Inflate(-8, 0)
        TextRenderer.DrawText(e.Graphics, GetItemText(Items(e.Index)), Font, r,
                              If(isSelected, Theme.TextMain, Theme.TextMuted),
                              TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        MyBase.WndProc(m)
        If m.Msg <> WM_PAINT Then Return

        ' Paint the closed box ourselves so no light borders or arrows remain
        Using g = Graphics.FromHwnd(Handle)
            g.Clear(If(Parent IsNot Nothing, Parent.BackColor, Theme.Back))
            Dim border = If(Focused OrElse DroppedDown, Theme.Accent, If(hovering, Theme.TextDim, Theme.Border))
            Theme.FillRounded(g, New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F), 6, Theme.Input, border)
            Dim text = If(SelectedIndex >= 0, GetItemText(SelectedItem), "")
            TextRenderer.DrawText(g, text, Font, New Rectangle(10, 0, Width - 40, Height),
                                  If(Enabled, Theme.TextMain, Theme.TextDim),
                                  TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
            TextRenderer.DrawText(g, ChrW(&HE70D), Theme.IconFontSmall, New Rectangle(Width - 30, 0, 22, Height),
                                  Theme.TextMuted, TextFormatFlags.VerticalCenter Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.NoPadding)
        End Using
    End Sub

End Class


''' <summary>Horizontal slider.</summary>
Friend Class Slider
    Inherits PaintedControl

    Private _min As Integer = 0
    Private _max As Integer = 100
    Private _value As Integer = 0
    Private dragging As Boolean

    Public Event ValueChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.Selectable, True)
        Cursor = Cursors.Hand
        Size = New System.Drawing.Size(240, 24)
    End Sub

    Public Property Minimum As Integer
        Get
            Return _min
        End Get
        Set(v As Integer)
            _min = v
            If _max < _min Then _max = _min
            Value = _value
            Invalidate()
        End Set
    End Property

    Public Property Maximum As Integer
        Get
            Return _max
        End Get
        Set(v As Integer)
            _max = Math.Max(v, _min)
            Value = _value
            Invalidate()
        End Set
    End Property

    Public Property SmallStep As Integer = 1

    Public Property Value As Integer
        Get
            Return _value
        End Get
        Set(v As Integer)
            Dim clamped = Math.Min(_max, Math.Max(_min, v))
            If clamped = _value Then Return
            _value = clamped
            Invalidate()
            RaiseEvent ValueChanged(Me, EventArgs.Empty)
        End Set
    End Property

    Private Function ValueFromX(x As Integer) As Integer
        Dim usable = Math.Max(1, Width - 16)
        Dim ratio = Math.Min(1.0, Math.Max(0.0, (x - 8) / CDbl(usable)))
        Dim raw = _min + ratio * (_max - _min)
        Dim stepSize = Math.Max(1, SmallStep)
        Return CInt(Math.Round(raw / stepSize)) * stepSize
    End Function

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        Focus()
        If e.Button = MouseButtons.Left Then
            dragging = True
            Value = ValueFromX(e.X)
        End If
        MyBase.OnMouseDown(e)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        If dragging Then Value = ValueFromX(e.X)
        MyBase.OnMouseMove(e)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        dragging = False
        MyBase.OnMouseUp(e)
    End Sub

    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        If keyData = Keys.Left OrElse keyData = Keys.Right Then Return True
        Return MyBase.IsInputKey(keyData)
    End Function

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.KeyCode = Keys.Left Then Value -= Math.Max(1, SmallStep)
        If e.KeyCode = Keys.Right Then Value += Math.Max(1, SmallStep)
        MyBase.OnKeyDown(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        Dim cy = Height / 2.0F
        Dim ratio = If(_max = _min, 0.0F, CSng((_value - _min) / CDbl(_max - _min)))
        Dim x = 8 + ratio * (Width - 16)

        Theme.FillRounded(g, New RectangleF(8, cy - 2, Width - 16, 4), 2, Theme.Border)
        Theme.FillRounded(g, New RectangleF(8, cy - 2, Math.Max(0, x - 8), 4), 2, If(Enabled, Theme.Accent, Theme.TextDim))

        g.SmoothingMode = SmoothingMode.AntiAlias
        Using knob As New SolidBrush(Color.White)
            g.FillEllipse(knob, x - 8, cy - 8, 16, 16)
        End Using
        Using ring As New Pen(If(Focused OrElse hovering, Theme.Accent, Theme.Border), 2)
            g.DrawEllipse(ring, x - 8, cy - 8, 16, 16)
        End Using
    End Sub

End Class


''' <summary>Thin progress bar (0-100).</summary>
Friend Class SlimProgress
    Inherits PaintedControl

    Private _value As Double

    Public Sub New()
        Size = New System.Drawing.Size(240, 6)
    End Sub

    Public Property Value As Double
        Get
            Return _value
        End Get
        Set(v As Double)
            _value = Math.Min(100, Math.Max(0, v))
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim r = Height / 2.0F
        Theme.FillRounded(e.Graphics, New RectangleF(0, 0, Width, Height), r, Theme.Border)
        Dim w = CSng(Width * _value / 100)
        If w > 0 Then Theme.FillRounded(e.Graphics, New RectangleF(0, 0, Math.Max(Height, w), Height), r, Theme.Accent)
    End Sub

End Class


''' <summary>One row for <see cref="EntryListBox"/>.</summary>
Friend Class ListEntry
    Public Property Title As String = ""
    Public Property Subtitle As String = ""
    Public Property Detail As String = ""
    Public Property Badge As String = ""
    Public Property BadgeColor As Color = Theme.Accent
    Public Property Dimmed As Boolean
    Public Property Tag As Object
    ''' <summary>Indentation level (16 px per level), e.g. for sub-forums.</summary>
    Public Property Indent As Integer
    ''' <summary>Section header row: drawn smaller and in the accent color.</summary>
    Public Property IsHeader As Boolean
    ''' <summary>Small dot in front of the title (e.g. unread).</summary>
    Public Property Marked As Boolean

    Public Overrides Function ToString() As String
        Return Title
    End Function
End Class

''' <summary>List with two-line rows, a right-aligned detail and an optional badge.</summary>
Friend Class EntryListBox
    Inherits ListBox

    Private Const WM_PAINT As Integer = &HF

    Public Property EmptyText As String = "Keine Einträge"

    Public Sub New()
        DrawMode = DrawMode.OwnerDrawFixed
        ItemHeight = 50
        BorderStyle = BorderStyle.None
        BackColor = Theme.Surface
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
        IntegralHeight = False
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Theme.UseDarkScrollbars(Me)
    End Sub

    Protected Overrides Sub OnDrawItem(e As DrawItemEventArgs)
        If e.Index < 0 OrElse e.Index >= Items.Count Then Return
        Dim g = e.Graphics
        Dim entry = TryCast(Items(e.Index), ListEntry)
        Dim isSelected = (e.State And DrawItemState.Selected) = DrawItemState.Selected

        Using b As New SolidBrush(Theme.Surface)
            g.FillRectangle(b, e.Bounds)
        End Using
        Dim card As New RectangleF(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Width - 4, e.Bounds.Height - 4)
        If isSelected Then
            Theme.FillRounded(g, card, 6, Theme.SurfaceHover, Theme.Accent)
        End If

        If entry Is Nothing Then
            TextRenderer.DrawText(g, Items(e.Index).ToString(), Font, Rectangle.Round(card), Theme.TextMain, TextFormatFlags.VerticalCenter)
            Return
        End If

        Dim left = e.Bounds.X + 14 + entry.Indent * 16
        If entry.IsHeader Then
            TextRenderer.DrawText(g, entry.Title.ToUpperInvariant(), Theme.SmallFont, New Rectangle(left, e.Bounds.Y, e.Bounds.Width - left - 10, e.Bounds.Height),
                                  Theme.Accent, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
            Return
        End If
        If entry.Marked Then
            Using dot As New SolidBrush(Theme.Accent)
                g.FillEllipse(dot, left - 10, e.Bounds.Y + 14, 6, 6)
            End Using
        End If
        Dim detailWidth = If(entry.Detail.Length > 0, TextRenderer.MeasureText(g, entry.Detail, Theme.SmallFont).Width + 4, 0)
        Dim right = e.Bounds.Right - 14 - detailWidth
        Dim titleColor = If(entry.Dimmed, Theme.TextDim, Theme.TextMain)

        Dim titleRect As New Rectangle(left, e.Bounds.Y + 7, Math.Max(10, right - left), 20)
        TextRenderer.DrawText(g, entry.Title, Theme.BodyBoldFont, titleRect, titleColor,
                              TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)

        If entry.Badge.Length > 0 Then
            Dim titleWidth = Math.Min(titleRect.Width - 60, TextRenderer.MeasureText(g, entry.Title, Theme.BodyBoldFont, System.Drawing.Size.Empty, TextFormatFlags.NoPadding).Width)
            Dim badgeSize = TextRenderer.MeasureText(g, entry.Badge, Theme.SmallFont, System.Drawing.Size.Empty, TextFormatFlags.NoPadding)
            Dim badgeRect As New Rectangle(left + titleWidth + 8, e.Bounds.Y + 9, badgeSize.Width + 12, 17)
            Theme.FillRounded(g, badgeRect, 8, Color.FromArgb(48, entry.BadgeColor))
            TextRenderer.DrawText(g, entry.Badge, Theme.SmallFont, badgeRect, entry.BadgeColor,
                                  TextFormatFlags.VerticalCenter Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.NoPadding)
        End If

        TextRenderer.DrawText(g, entry.Subtitle, Theme.SmallFont, New Rectangle(left, e.Bounds.Y + 27, Math.Max(10, right - left), 17),
                              Theme.TextMuted, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)

        If detailWidth > 0 Then
            TextRenderer.DrawText(g, entry.Detail, Theme.SmallFont, New Rectangle(right, e.Bounds.Y, detailWidth, e.Bounds.Height),
                                  Theme.TextMuted, TextFormatFlags.VerticalCenter Or TextFormatFlags.Right Or TextFormatFlags.NoPadding)
        End If
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        MyBase.WndProc(m)
        If m.Msg = WM_PAINT AndAlso Items.Count = 0 AndAlso EmptyText.Length > 0 Then
            Using g = Graphics.FromHwnd(Handle)
                TextRenderer.DrawText(g, EmptyText, Theme.BodyFont, ClientRectangle, Theme.TextDim,
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.WordBreak)
            End Using
        End If
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        Invalidate()
    End Sub

End Class


''' <summary>Panel that scrolls with dark scrollbars.</summary>
Friend Class ScrollPanel
    Inherits Panel

    Public Sub New()
        AutoScroll = True
        BackColor = Theme.Back
        DoubleBuffered = True
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Theme.UseDarkScrollbars(Me)
    End Sub

    Public Sub ScrollBy(delta As Integer)
        Dim newY = Math.Max(0, -AutoScrollPosition.Y - delta)
        AutoScrollPosition = New Point(0, newY)
    End Sub

End Class


''' <summary>Read-only colored log output.</summary>
Friend Class ConsoleBox
    Inherits RichTextBox

    Private Const MaxLines As Integer = 4000

    Public Sub New()
        BorderStyle = BorderStyle.None
        BackColor = Theme.Input
        ForeColor = Theme.TextMain
        Font = Theme.MonoFont
        [ReadOnly] = True
        DetectUrls = False
        WordWrap = True
        ScrollBars = RichTextBoxScrollBars.Vertical
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Theme.UseDarkScrollbars(Me)
    End Sub

    Public Sub AppendLine(text As String, lineColor As Color)
        If GetLineFromCharIndex(TextLength) > MaxLines Then
            ' Remove the oldest quarter at once to keep it fast
            [ReadOnly] = False
            [Select](0, GetFirstCharIndexFromLine(MaxLines \ 4))
            SelectedText = ""
            [ReadOnly] = True
        End If
        SelectionStart = TextLength
        SelectionLength = 0
        SelectionColor = lineColor
        AppendText(text & Environment.NewLine)
        SelectionColor = ForeColor
        ScrollToCaret()
    End Sub

End Class
