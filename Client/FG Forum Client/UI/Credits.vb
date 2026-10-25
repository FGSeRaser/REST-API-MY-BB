Imports System.Drawing.Drawing2D

''' <summary>Author of the program and the link to the GitHub profile.</summary>
Friend NotInheritable Class AppInfo

    Private Sub New()
    End Sub

    Public Const Author As String = "FGSeRaser"
    Public Const GitHubUrl As String = "https://github.com/FGSeRaser"
    Public Const AppName As String = "FG Forum Client"
    Public Const Version As String = "1.0.0"
    Public Const ProjectUrl As String = "https://github.com/FGSeRaser/REST-API-MY-BB"

    ''' <summary>Opens the GitHub profile in the default browser.</summary>
    Public Shared Sub OpenGitHub()
        OpenUrl(GitHubUrl)
    End Sub

    ''' <summary>Opens a web address in the default browser.</summary>
    Public Shared Sub OpenUrl(url As String)
        Try
            Process.Start(New ProcessStartInfo(url) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show("Der Browser konnte nicht geöffnet werden." & Environment.NewLine & url,
                            AppName, MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Try
    End Sub

End Class


''' <summary>Clickable "von FGSeRaser" entry in the sidebar that opens the GitHub profile.</summary>
Friend Class GitHubLink
    Inherits PaintedControl

    Private ReadOnly linkTip As New ToolTip()

    Public Sub New()
        Cursor = Cursors.Hand
        Size = New System.Drawing.Size(198, 46)
        BackColor = Theme.Sidebar
        linkTip.SetToolTip(Me, AppInfo.GitHubUrl)
    End Sub

    Protected Overrides Sub OnClick(e As EventArgs)
        AppInfo.OpenGitHub()
        MyBase.OnClick(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        If hovering Then Theme.FillRounded(g, New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F), 8, Theme.SurfaceHover)

        ' Round avatar with the initials
        Dim avatar As New Rectangle(10, (Height - 30) \ 2, 30, 30)
        Using avatarBrush As New SolidBrush(Theme.AccentSoft)
            g.FillEllipse(avatarBrush, avatar)
        End Using
        TextRenderer.DrawText(g, "FG", Theme.BodyBoldFont, avatar, Theme.Accent,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

        TextRenderer.DrawText(g, "von " & AppInfo.Author, Theme.BodyBoldFont, New Point(50, 7), Theme.TextMain, TextFormatFlags.NoPadding)
        TextRenderer.DrawText(g, "github.com/" & AppInfo.Author, Theme.SmallFont, New Point(50, 26),
                              If(hovering, Theme.Accent, Theme.TextMuted), TextFormatFlags.NoPadding)
        ' Arrow "opens outside"
        TextRenderer.DrawText(g, ChrW(&HE8A7), Theme.IconFontSmall, New Rectangle(Width - 30, 0, 22, Height),
                              If(hovering, Theme.Accent, Theme.TextDim),
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then linkTip.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
