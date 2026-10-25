Imports System.Threading

Friend Module Program

    <STAThread>
    Sub Main()
        ' Only one instance (autostart plus manual start)
        Dim createdNew As Boolean
        Using mutex As New Mutex(True, "FGForumClient_SingleInstance_5C21", createdNew)
            If Not createdNew Then
                MessageBox.Show("Der FG Forum Client läuft bereits. Du findest ihn im Infobereich der Taskleiste.", AppInfo.AppName,
                                MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            HtmlView.EnableModernRendering()
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            Dim startInTray = Environment.GetCommandLineArgs().Any(Function(a) String.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase))
            Application.Run(New MainForm(startInTray))
        End Using
    End Sub

End Module
