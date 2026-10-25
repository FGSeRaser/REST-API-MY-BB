Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Runtime.InteropServices

''' <summary>Colors, fonts, icons and drawing helpers for the dark UI.</summary>
Friend NotInheritable Class Theme

    Private Sub New()
    End Sub

    ' ---------- Colors ----------
    Public Shared ReadOnly Back As Color = Color.FromArgb(20, 21, 24)
    Public Shared ReadOnly Sidebar As Color = Color.FromArgb(14, 15, 17)
    Public Shared ReadOnly Surface As Color = Color.FromArgb(29, 30, 34)
    Public Shared ReadOnly SurfaceHover As Color = Color.FromArgb(38, 40, 45)
    Public Shared ReadOnly Input As Color = Color.FromArgb(22, 23, 26)
    Public Shared ReadOnly Border As Color = Color.FromArgb(50, 52, 58)
    Public Shared ReadOnly Accent As Color = Color.FromArgb(77, 163, 255)
    Public Shared ReadOnly AccentHover As Color = Color.FromArgb(110, 182, 255)
    Public Shared ReadOnly AccentPressed As Color = Color.FromArgb(52, 132, 224)
    Public Shared ReadOnly AccentSoft As Color = Color.FromArgb(22, 44, 72)
    Public Shared ReadOnly Danger As Color = Color.FromArgb(229, 83, 75)
    Public Shared ReadOnly Warning As Color = Color.FromArgb(238, 182, 64)
    Public Shared ReadOnly Info As Color = Color.FromArgb(98, 160, 234)
    Public Shared ReadOnly OnAccent As Color = Color.FromArgb(255, 255, 255)
    Public Shared ReadOnly Good As Color = Color.FromArgb(92, 184, 96)
    Public Shared ReadOnly TextMain As Color = Color.FromArgb(235, 236, 240)
    Public Shared ReadOnly TextMuted As Color = Color.FromArgb(140, 143, 152)
    Public Shared ReadOnly TextDim As Color = Color.FromArgb(96, 99, 108)

    ' ---------- Fonts ----------
    Public Shared ReadOnly TitleFont As Font = PickFont(17.0F, FontStyle.Regular, "Segoe UI Semibold", "Segoe UI")
    Public Shared ReadOnly HeadingFont As Font = PickFont(10.5F, FontStyle.Regular, "Segoe UI Semibold", "Segoe UI")
    Public Shared ReadOnly BodyFont As Font = PickFont(9.5F, FontStyle.Regular, "Segoe UI")
    Public Shared ReadOnly BodyBoldFont As Font = PickFont(9.5F, FontStyle.Regular, "Segoe UI Semibold", "Segoe UI")
    Public Shared ReadOnly SmallFont As Font = PickFont(8.25F, FontStyle.Regular, "Segoe UI")
    Public Shared ReadOnly MonoFont As Font = PickFont(9.5F, FontStyle.Regular, "Cascadia Mono", "Consolas", "Courier New")
    Public Shared ReadOnly IconFont As Font = PickFont(11.0F, FontStyle.Regular, "Segoe Fluent Icons", "Segoe MDL2 Assets")
    Public Shared ReadOnly IconFontSmall As Font = PickFont(9.0F, FontStyle.Regular, "Segoe Fluent Icons", "Segoe MDL2 Assets")

    ' ---------- Icon glyphs (Segoe MDL2 Assets / Segoe Fluent Icons) ----------
    Public Const IconPlay As String = ChrW(&HE768)
    Public Const IconStop As String = ChrW(&HE71A)
    Public Const IconDownload As String = ChrW(&HE896)
    Public Const IconPlugins As String = ChrW(&HE71D)
    Public Const IconSettings As String = ChrW(&HE713)
    Public Const IconFolder As String = ChrW(&HE8B7)
    Public Const IconRefresh As String = ChrW(&HE72C)
    Public Const IconAdd As String = ChrW(&HE710)
    Public Const IconDelete As String = ChrW(&HE74D)
    Public Const IconSearch As String = ChrW(&HE721)
    Public Const IconSave As String = ChrW(&HE74E)
    Public Const IconMap As String = ChrW(&HE707)
    Public Const IconCalc As String = ChrW(&HE8EF)
    Public Const IconPeople As String = ChrW(&HE716)
    Public Const IconServer As String = ChrW(&HE968)
    Public Const IconApps As String = ChrW(&HE74C)
    Public Const IconOverlay As String = ChrW(&HE7F4)
    Public Const IconConsole As String = ChrW(&HE756)
    Public Const IconList As String = ChrW(&HEA37)
    Public Const IconOpen As String = ChrW(&HE8A7)
    Public Const IconHome As String = ChrW(&HE80F)
    Public Const IconForum As String = ChrW(&HE8F2)
    Public Const IconMail As String = ChrW(&HE715)
    Public Const IconAccount As String = ChrW(&HE77B)
    Public Const IconEdit As String = ChrW(&HE70F)
    Public Const IconSend As String = ChrW(&HE724)
    Public Const IconBack As String = ChrW(&HE72B)
    Public Const IconForward As String = ChrW(&HE72A)
    Public Const IconAttach As String = ChrW(&HE723)
    Public Const IconLock As String = ChrW(&HE72E)
    Public Const IconUnlock As String = ChrW(&HE785)
    Public Const IconPin As String = ChrW(&HE718)
    Public Const IconReply As String = ChrW(&HE97A)
    Public Const IconKey As String = ChrW(&HE8D7)
    Public Const IconRocket As String = ChrW(&HE7C1)
    Public Const IconPreview As String = ChrW(&HE890)
    Public Const IconBell As String = ChrW(&HEA8F)

    Private Shared installedFamilies As HashSet(Of String)

    ''' <summary>Returns the first installed font family from the list, otherwise the system UI font.</summary>
    Public Shared Function PickFont(size As Single, style As FontStyle, ParamArray families As String()) As Font
        If installedFamilies Is Nothing Then
            installedFamilies = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Using installed As New InstalledFontCollection()
                For Each family As FontFamily In installed.Families
                    installedFamilies.Add(family.Name)
                Next
            End Using
        End If

        For Each familyName As String In families
            If installedFamilies.Contains(familyName) Then Return New Font(familyName, size, style)
        Next
        Return New Font(SystemFonts.MessageBoxFont.FontFamily, size, style)
    End Function

    Public Shared Function RoundedRect(rect As RectangleF, radius As Single) As GraphicsPath
        Dim path As New GraphicsPath()
        If radius <= 0 Then
            path.AddRectangle(rect)
            Return path
        End If
        Dim d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height))
        path.AddArc(rect.X, rect.Y, d, d, 180, 90)
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90)
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90)
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90)
        path.CloseFigure()
        Return path
    End Function

    Public Shared Sub FillRounded(g As Graphics, rect As RectangleF, radius As Single, fill As Color, Optional border As Color = Nothing, Optional borderWidth As Single = 1.0F)
        Dim oldMode = g.SmoothingMode
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using path = RoundedRect(rect, radius)
            Using b As New SolidBrush(fill)
                g.FillPath(b, path)
            End Using
            If border <> Color.Empty AndAlso border.A > 0 Then
                Using p As New Pen(border, borderWidth)
                    g.DrawPath(p, path)
                End Using
            End If
        End Using
        g.SmoothingMode = oldMode
    End Sub

    Public Shared Function Blend(a As Color, b As Color, amount As Double) As Color
        Return Color.FromArgb(
            CInt(a.R + (CInt(b.R) - a.R) * amount),
            CInt(a.G + (CInt(b.G) - a.G) * amount),
            CInt(a.B + (CInt(b.B) - a.B) * amount))
    End Function

    ' ---------- Native helpers ----------

    <DllImport("uxtheme.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function SetWindowTheme(hwnd As IntPtr, appName As String, idList As String) As Integer
    End Function

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
    End Function

    ''' <summary>Gives scrollbars of a control the dark Windows 10/11 look.</summary>
    Public Shared Sub UseDarkScrollbars(target As Control)
        Try
            If target.IsHandleCreated Then
                SetWindowTheme(target.Handle, "DarkMode_Explorer", Nothing)
            Else
                AddHandler target.HandleCreated, Sub(s, e) SetWindowTheme(DirectCast(s, Control).Handle, "DarkMode_Explorer", Nothing)
            End If
        Catch
            ' Older Windows: keep the default scrollbars
        End Try
    End Sub

    ''' <summary>Dark title bar on Windows 10 20H1+ and Windows 11.</summary>
    Public Shared Sub UseDarkTitleBar(form As Form)
        Try
            Dim enabledValue = 1
            If DwmSetWindowAttribute(form.Handle, 20, enabledValue, 4) <> 0 Then
                DwmSetWindowAttribute(form.Handle, 19, enabledValue, 4)
            End If
        Catch
        End Try
    End Sub

End Class
