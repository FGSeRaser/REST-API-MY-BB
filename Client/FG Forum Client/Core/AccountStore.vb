Imports System.Security.Cryptography
Imports System.Text
Imports Microsoft.Win32

''' <summary>One saved login (forum + user). The secret is encrypted with Windows (DPAPI).</summary>
Friend Class Account
    Public Property Id As String = Guid.NewGuid().ToString("N")
    Public Property ForumUrl As String = ""
    Public Property ForumName As String = ""
    Public Property Username As String = ""
    Public Property Uid As Integer
    Public Property Avatar As String = ""
    ''' <summary>"session" (login with password) or "key" (API key).</summary>
    Public Property Kind As String = "session"
    Public Property ProtectedSecret As String = ""
    Public Property LastSeenPid As Integer
    Public Property LastUnreadPms As Integer

    Public ReadOnly Property IsKey As Boolean
        Get
            Return Kind = "key"
        End Get
    End Property

    Public ReadOnly Property DisplayName As String
        Get
            Return Username & " · " & If(ForumName.Length > 0, ForumName, ForumUrl)
        End Get
    End Property

    Public Property Secret As String
        Get
            Return AccountStore.Unprotect(ProtectedSecret)
        End Get
        Set(value As String)
            ProtectedSecret = AccountStore.Protect(value)
        End Set
    End Property

    Public Function CreateClient() As ApiClient
        Return New ApiClient(ForumUrl) With {.Secret = Secret, .SecretIsKey = IsKey}
    End Function
End Class


''' <summary>Client settings.</summary>
Friend Class ClientSettings
    Public Property LatestCount As Integer = 10
    Public Property PollMinutes As Integer = 2
    Public Property Notifications As Boolean = True
    Public Property MinimizeToTray As Boolean = True
    Public Property PostsPerPage As Integer = 20
    Public Property ShowSignatures As Boolean = True
    Public Property LivePreview As Boolean = True
End Class


''' <summary>Accounts and settings in %AppData%\FG Forum Client\config.json.</summary>
Friend Class AccountStore

    Public Property Accounts As New List(Of Account)()
    Public Property ActiveId As String = ""
    Public Property Settings As New ClientSettings()

    Public Shared ReadOnly DataFolder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FG Forum Client")
    Private Shared ReadOnly FilePath As String = Path.Combine(DataFolder, "config.json")
    Private Shared ReadOnly Entropy As Byte() = Encoding.UTF8.GetBytes("FGSeRaser.ForumClient.v1")

    Private Const RunKey As String = "Software\Microsoft\Windows\CurrentVersion\Run"
    Private Const RunValue As String = "FG Forum Client"

    Public ReadOnly Property Active As Account
        Get
            Dim found = Accounts.FirstOrDefault(Function(a) a.Id = ActiveId)
            If found Is Nothing AndAlso Accounts.Count > 0 Then
                found = Accounts(0)
                ActiveId = found.Id
            End If
            Return found
        End Get
    End Property

    ' ---------- Load / save ----------

    Public Shared Function Load() As AccountStore
        Dim store As New AccountStore()
        Try
            If Not File.Exists(FilePath) Then Return store
            Dim root = Json.Parse(File.ReadAllText(FilePath, Encoding.UTF8))
            store.ActiveId = Json.GetString(root, "active")
            For Each item As Object In Json.GetArray(root, "accounts")
                store.Accounts.Add(New Account With {
                    .Id = Json.GetString(item, "id", Guid.NewGuid().ToString("N")),
                    .ForumUrl = Json.GetString(item, "forum"),
                    .ForumName = Json.GetString(item, "forumName"),
                    .Username = Json.GetString(item, "username"),
                    .Uid = Json.GetInt(item, "uid"),
                    .Avatar = Json.GetString(item, "avatar"),
                    .Kind = Json.GetString(item, "kind", "session"),
                    .ProtectedSecret = Json.GetString(item, "secret"),
                    .LastSeenPid = Json.GetInt(item, "lastSeenPid"),
                    .LastUnreadPms = Json.GetInt(item, "lastUnreadPms")})
            Next
            Dim s = Json.GetObject(root, "settings")
            If s IsNot Nothing Then
                store.Settings.LatestCount = Clamp(Json.GetInt(s, "latestCount", 10), 5, 50)
                store.Settings.PollMinutes = Clamp(Json.GetInt(s, "pollMinutes", 2), 0, 60)
                store.Settings.Notifications = Json.GetBool(s, "notifications", True)
                store.Settings.MinimizeToTray = Json.GetBool(s, "minimizeToTray", True)
                store.Settings.PostsPerPage = Clamp(Json.GetInt(s, "postsPerPage", 20), 5, 50)
                store.Settings.ShowSignatures = Json.GetBool(s, "showSignatures", True)
                store.Settings.LivePreview = Json.GetBool(s, "livePreview", True)
            End If
        Catch
            ' Broken file: start empty
        End Try
        Return store
    End Function

    Public Sub Save()
        Try
            Dim accountList As New List(Of Object)()
            For Each a As Account In Accounts
                accountList.Add(New Dictionary(Of String, Object) From {
                    {"id", a.Id}, {"forum", a.ForumUrl}, {"forumName", a.ForumName}, {"username", a.Username},
                    {"uid", a.Uid}, {"avatar", a.Avatar}, {"kind", a.Kind}, {"secret", a.ProtectedSecret},
                    {"lastSeenPid", a.LastSeenPid}, {"lastUnreadPms", a.LastUnreadPms}})
            Next
            Dim root As New Dictionary(Of String, Object) From {
                {"active", ActiveId},
                {"accounts", accountList},
                {"settings", New Dictionary(Of String, Object) From {
                    {"latestCount", Settings.LatestCount}, {"pollMinutes", Settings.PollMinutes},
                    {"notifications", Settings.Notifications}, {"minimizeToTray", Settings.MinimizeToTray},
                    {"postsPerPage", Settings.PostsPerPage}, {"showSignatures", Settings.ShowSignatures},
                    {"livePreview", Settings.LivePreview}}}}
            Directory.CreateDirectory(DataFolder)
            File.WriteAllText(FilePath, Json.Serialize(root), New UTF8Encoding(False))
        Catch
        End Try
    End Sub

    Private Shared Function Clamp(value As Integer, min As Integer, max As Integer) As Integer
        Return Math.Max(min, Math.Min(max, value))
    End Function

    ' ---------- Encryption (only this Windows user can read the secrets) ----------

    Public Shared Function Protect(plain As String) As String
        If String.IsNullOrEmpty(plain) Then Return ""
        Try
            Dim data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser)
            Return Convert.ToBase64String(data)
        Catch
            Return ""
        End Try
    End Function

    Public Shared Function Unprotect(encrypted As String) As String
        If String.IsNullOrEmpty(encrypted) Then Return ""
        Try
            Dim data = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), Entropy, DataProtectionScope.CurrentUser)
            Return Encoding.UTF8.GetString(data)
        Catch
            Return ""
        End Try
    End Function

    ' ---------- Autostart ----------

    Public Shared Property StartWithWindows As Boolean
        Get
            Try
                Using key = Registry.CurrentUser.OpenSubKey(RunKey, False)
                    Return key IsNot Nothing AndAlso key.GetValue(RunValue) IsNot Nothing
                End Using
            Catch
                Return False
            End Try
        End Get
        Set(value As Boolean)
            Try
                Using key = Registry.CurrentUser.OpenSubKey(RunKey, True)
                    If key Is Nothing Then Return
                    If value Then
                        key.SetValue(RunValue, """" & Application.ExecutablePath & """ --tray")
                    Else
                        key.DeleteValue(RunValue, False)
                    End If
                End Using
            Catch
            End Try
        End Set
    End Property

End Class


''' <summary>Drafts are saved automatically, so nothing gets lost when the client closes.</summary>
Friend NotInheritable Class Drafts

    Private Sub New()
    End Sub

    Private Shared ReadOnly Folder As String = Path.Combine(AccountStore.DataFolder, "drafts")

    Private Shared Function FileFor(key As String) As String
        Dim safe As New StringBuilder()
        For Each c As Char In key
            safe.Append(If(Char.IsLetterOrDigit(c) OrElse c = "-"c OrElse c = "_"c, c, "_"c))
        Next
        Return Path.Combine(Folder, safe.ToString() & ".json")
    End Function

    Public Shared Sub Save(key As String, subject As String, message As String)
        Try
            If String.IsNullOrWhiteSpace(message) AndAlso String.IsNullOrWhiteSpace(subject) Then
                Delete(key)
                Return
            End If
            Directory.CreateDirectory(Folder)
            Dim data As New Dictionary(Of String, Object) From {
                {"subject", If(subject, "")}, {"message", If(message, "")}, {"saved", DateTime.Now.ToString("o")}}
            File.WriteAllText(FileFor(key), Json.Serialize(data), New UTF8Encoding(False))
        Catch
        End Try
    End Sub

    ''' <summary>Returns (subject, message) or Nothing.</summary>
    Public Shared Function Load(key As String) As Tuple(Of String, String)
        Try
            Dim draftFile = FileFor(key)
            If Not IO.File.Exists(draftFile) Then Return Nothing
            Dim data = Json.Parse(IO.File.ReadAllText(draftFile, Encoding.UTF8))
            Return Tuple.Create(Json.GetString(data, "subject"), Json.GetString(data, "message"))
        Catch
            Return Nothing
        End Try
    End Function

    Public Shared Sub Delete(key As String)
        Try
            Dim draftFile = FileFor(key)
            If IO.File.Exists(draftFile) Then IO.File.Delete(draftFile)
        Catch
        End Try
    End Sub

End Class
