Imports Microsoft.Win32

''' <summary>Arguments for an "fg://action/argument" link in an <see cref="HtmlView"/>.</summary>
Friend Class HtmlActionEventArgs
    Inherits EventArgs

    Public ReadOnly Property Action As String
    Public ReadOnly Property Argument As String

    Public Sub New(actionName As String, actionArgument As String)
        Action = actionName
        Argument = actionArgument
    End Sub

    Public ReadOnly Property Id As Integer
        Get
            Dim value As Integer
            Integer.TryParse(Argument, value)
            Return value
        End Get
    End Property
End Class


''' <summary>
''' Shows HTML from <see cref="HtmlBuilder"/>. Normal links open in the default browser,
''' "fg://" links raise <see cref="ActionClicked"/>.
''' </summary>
Friend Class HtmlView
    Inherits Panel

    Private ReadOnly browser As New WebBrowser()
    Private pendingHtml As String

    Public Event ActionClicked As EventHandler(Of HtmlActionEventArgs)
    ''' <summary>Raised when a page has finished loading (e.g. to load images).</summary>
    Public Event PageLoaded As EventHandler

    Public Sub New()
        BackColor = Theme.Back
        Padding = New Padding(0)
        browser.Dock = DockStyle.Fill
        browser.ScriptErrorsSuppressed = True
        browser.AllowWebBrowserDrop = False
        browser.IsWebBrowserContextMenuEnabled = True
        browser.WebBrowserShortcutsEnabled = True
        AddHandler browser.Navigating, AddressOf Browser_Navigating
        AddHandler browser.NewWindow, Sub(s, e) e.Cancel = True
        AddHandler browser.DocumentCompleted, AddressOf Browser_DocumentCompleted
        Controls.Add(browser)
    End Sub

    ''' <summary>
    ''' The WebBrowser control uses an old IE mode by default.
    ''' This switches the program to IE11 mode (only for this program, no admin rights needed).
    ''' </summary>
    Public Shared Sub EnableModernRendering()
        Try
            Dim exeName = Path.GetFileName(Application.ExecutablePath)
            Using key = Registry.CurrentUser.CreateSubKey("Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION")
                key.SetValue(exeName, 11001, RegistryValueKind.DWord)
            End Using
            Using key = Registry.CurrentUser.CreateSubKey("Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_GPU_RENDERING")
                key.SetValue(exeName, 1, RegistryValueKind.DWord)
            End Using
        Catch
            ' Not critical
        End Try
    End Sub

    Public Sub ShowHtml(html As String)
        pendingHtml = html
        Try
            browser.DocumentText = html
        Catch
        End Try
    End Sub

    Public ReadOnly Property Document As HtmlDocument
        Get
            Return browser.Document
        End Get
    End Property

    ''' <summary>Scrolls to an element id (e.g. "pid123").</summary>
    Public Sub ScrollTo(elementId As String)
        Try
            Dim element = browser.Document?.GetElementById(elementId)
            If element IsNot Nothing Then element.ScrollIntoView(True)
        Catch
        End Try
    End Sub

    Public Sub ScrollToBottom()
        Try
            Dim body = browser.Document?.Body
            If body IsNot Nothing Then browser.Document.Window.ScrollTo(0, body.ScrollRectangle.Height)
        Catch
        End Try
    End Sub

    Private Sub Browser_Navigating(sender As Object, e As WebBrowserNavigatingEventArgs)
        Dim url = e.Url.ToString()
        If url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) Then Return

        e.Cancel = True
        If url.StartsWith("fg:", StringComparison.OrdinalIgnoreCase) Then
            ' fg://action/argument
            Dim rest = url.Substring(3).TrimStart("/"c)
            Dim slash = rest.IndexOf("/"c)
            Dim action = If(slash >= 0, rest.Substring(0, slash), rest).ToLowerInvariant()
            Dim argument = If(slash >= 0, Uri.UnescapeDataString(rest.Substring(slash + 1)).TrimEnd("/"c), "")
            RaiseActionClicked(action, argument)
        ElseIf url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) OrElse url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
            AppInfo.OpenUrl(url)
        End If
    End Sub

    Private Sub RaiseActionClicked(action As String, argument As String)
        RaiseEvent ActionClicked(Me, New HtmlActionEventArgs(action, argument))
    End Sub

    Private Sub Browser_DocumentCompleted(sender As Object, e As WebBrowserDocumentCompletedEventArgs)
        RaiseEvent PageLoaded(Me, EventArgs.Empty)
    End Sub

    ''' <summary>All images that wait for an attachment download (data-aid attribute).</summary>
    Public Function PendingImages() As List(Of HtmlElement)
        Dim result As New List(Of HtmlElement)()
        Try
            If browser.Document Is Nothing Then Return result
            For Each element As HtmlElement In browser.Document.GetElementsByTagName("img")
                Dim aid = element.GetAttribute("data-aid")
                If Not String.IsNullOrEmpty(aid) AndAlso element.GetAttribute("src").StartsWith("data:image/gif", StringComparison.Ordinal) Then
                    result.Add(element)
                End If
            Next
        Catch
        End Try
        Return result
    End Function

End Class
