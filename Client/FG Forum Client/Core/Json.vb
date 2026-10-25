Imports System.Collections
Imports System.Globalization
Imports System.Text

''' <summary>
''' Small JSON reader, so the project needs no extra NuGet package.
''' Objects become Dictionary(Of String, Object), arrays List(Of Object),
''' numbers Double, plus String, Boolean and Nothing.
''' </summary>
Friend NotInheritable Class Json

    Private ReadOnly src As String
    Private pos As Integer

    Private Sub New(text As String)
        src = text
    End Sub

    Public Shared Function Parse(text As String) As Object
        Dim reader As New Json(text)
        reader.SkipWhitespace()
        Dim result = reader.ReadValue()
        Return result
    End Function

    ' ---------- Convenience accessors ----------

    ''' <summary>Value of a key in a JSON object, or Nothing.</summary>
    Public Shared Function GetValue(obj As Object, key As String) As Object
        Dim dict = TryCast(obj, Dictionary(Of String, Object))
        If dict Is Nothing Then Return Nothing
        Dim value As Object = Nothing
        dict.TryGetValue(key, value)
        Return value
    End Function

    Public Shared Function GetString(obj As Object, key As String, Optional fallback As String = "") As String
        Dim value = GetValue(obj, key)
        If value Is Nothing Then Return fallback
        If TypeOf value Is Double Then Return DirectCast(value, Double).ToString(CultureInfo.InvariantCulture)
        Return value.ToString()
    End Function

    Public Shared Function GetNumber(obj As Object, key As String, Optional fallback As Double = 0) As Double
        Dim value = GetValue(obj, key)
        If TypeOf value Is Double Then Return DirectCast(value, Double)
        Return fallback
    End Function

    Public Shared Function GetArray(obj As Object, key As String) As List(Of Object)
        Return If(TryCast(GetValue(obj, key), List(Of Object)), New List(Of Object)())
    End Function

    Public Shared Function GetObject(obj As Object, key As String) As Dictionary(Of String, Object)
        Return TryCast(GetValue(obj, key), Dictionary(Of String, Object))
    End Function

    ''' <summary>Escapes a string for use inside a JSON string literal.</summary>
    Public Shared Function Quote(value As String) As String
        Dim sb As New StringBuilder("""")
        For Each c As Char In value
            Select Case c
                Case """"c : sb.Append("\""")
                Case "\"c : sb.Append("\\")
                Case ChrW(10) : sb.Append("\n")
                Case ChrW(13) : sb.Append("\r")
                Case ChrW(9) : sb.Append("\t")
                Case Else
                    If AscW(c) < 32 Then
                        sb.Append("\u").Append(AscW(c).ToString("x4"))
                    Else
                        sb.Append(c)
                    End If
            End Select
        Next
        Return sb.Append("""").ToString()
    End Function

    ''' <summary>
    ''' Writes .NET values as JSON: dictionaries, lists, strings, numbers, booleans and Nothing.
    ''' </summary>
    Public Shared Function Serialize(value As Object) As String
        Dim sb As New StringBuilder()
        WriteValue(sb, value)
        Return sb.ToString()
    End Function

    Private Shared Sub WriteValue(sb As StringBuilder, value As Object)
        If value Is Nothing Then
            sb.Append("null")
        ElseIf TypeOf value Is String Then
            sb.Append(Quote(DirectCast(value, String)))
        ElseIf TypeOf value Is Boolean Then
            sb.Append(If(DirectCast(value, Boolean), "true", "false"))
        ElseIf TypeOf value Is Integer OrElse TypeOf value Is Long OrElse TypeOf value Is Short OrElse TypeOf value Is Byte Then
            sb.Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture))
        ElseIf TypeOf value Is Double OrElse TypeOf value Is Single OrElse TypeOf value Is Decimal Then
            sb.Append(Convert.ToDouble(value).ToString("R", CultureInfo.InvariantCulture))
        ElseIf TypeOf value Is IDictionary(Of String, Object) Then
            sb.Append("{")
            Dim first = True
            For Each pair As KeyValuePair(Of String, Object) In DirectCast(value, IDictionary(Of String, Object))
                If Not first Then sb.Append(",")
                first = False
                sb.Append(Quote(pair.Key)).Append(":")
                WriteValue(sb, pair.Value)
            Next
            sb.Append("}")
        ElseIf TypeOf value Is IEnumerable Then
            sb.Append("[")
            Dim first = True
            For Each element As Object In DirectCast(value, IEnumerable)
                If Not first Then sb.Append(",")
                first = False
                WriteValue(sb, element)
            Next
            sb.Append("]")
        Else
            sb.Append(Quote(value.ToString()))
        End If
    End Sub

    ''' <summary>Integer value of a key (JSON numbers are Double).</summary>
    Public Shared Function GetInt(obj As Object, key As String, Optional fallback As Integer = 0) As Integer
        Dim value = GetValue(obj, key)
        If TypeOf value Is Double Then Return CInt(Math.Round(DirectCast(value, Double)))
        If TypeOf value Is String Then
            Dim parsed As Integer
            If Integer.TryParse(DirectCast(value, String), parsed) Then Return parsed
        End If
        Return fallback
    End Function

    Public Shared Function GetLong(obj As Object, key As String, Optional fallback As Long = 0) As Long
        Dim value = GetValue(obj, key)
        If TypeOf value Is Double Then Return CLng(Math.Round(DirectCast(value, Double)))
        Return fallback
    End Function

    Public Shared Function GetBool(obj As Object, key As String, Optional fallback As Boolean = False) As Boolean
        Dim value = GetValue(obj, key)
        If TypeOf value Is Boolean Then Return DirectCast(value, Boolean)
        If TypeOf value Is Double Then Return DirectCast(value, Double) <> 0
        Return fallback
    End Function

    ' ---------- Parser ----------

    Private Function ReadValue() As Object
        If pos >= src.Length Then Throw New FormatException("Unexpected end of JSON")
        Dim c = src(pos)
        Select Case c
            Case "{"c : Return ReadObject()
            Case "["c : Return ReadArray()
            Case """"c : Return ReadString()
            Case "t"c : Expect("true") : Return True
            Case "f"c : Expect("false") : Return False
            Case "n"c : Expect("null") : Return Nothing
            Case Else : Return ReadNumber()
        End Select
    End Function

    Private Function ReadObject() As Dictionary(Of String, Object)
        Dim result As New Dictionary(Of String, Object)(StringComparer.Ordinal)
        pos += 1 ' {
        SkipWhitespace()
        If Peek() = "}"c Then
            pos += 1
            Return result
        End If
        Do
            SkipWhitespace()
            Dim key = ReadString()
            SkipWhitespace()
            Expect(":")
            SkipWhitespace()
            result(key) = ReadValue()
            SkipWhitespace()
            Dim c = NextChar()
            If c = "}"c Then Return result
            If c <> ","c Then Throw New FormatException("Expected , or } in JSON object")
        Loop
    End Function

    Private Function ReadArray() As List(Of Object)
        Dim result As New List(Of Object)()
        pos += 1 ' [
        SkipWhitespace()
        If Peek() = "]"c Then
            pos += 1
            Return result
        End If
        Do
            SkipWhitespace()
            result.Add(ReadValue())
            SkipWhitespace()
            Dim c = NextChar()
            If c = "]"c Then Return result
            If c <> ","c Then Throw New FormatException("Expected , or ] in JSON array")
        Loop
    End Function

    Private Function ReadString() As String
        If NextChar() <> """"c Then Throw New FormatException("Expected string in JSON")
        Dim sb As New StringBuilder()
        Do
            Dim c = NextChar()
            If c = """"c Then Return sb.ToString()
            If c <> "\"c Then
                sb.Append(c)
                Continue Do
            End If
            Dim esc = NextChar()
            Select Case esc
                Case "n"c : sb.Append(ChrW(10))
                Case "r"c : sb.Append(ChrW(13))
                Case "t"c : sb.Append(ChrW(9))
                Case "b"c : sb.Append(ChrW(8))
                Case "f"c : sb.Append(ChrW(12))
                Case "u"c
                    If pos + 4 > src.Length Then Throw New FormatException("Bad unicode escape in JSON")
                    sb.Append(ChrW(Integer.Parse(src.Substring(pos, 4), NumberStyles.HexNumber)))
                    pos += 4
                Case Else : sb.Append(esc) ' \" \\ \/
            End Select
        Loop
    End Function

    Private Function ReadNumber() As Double
        Dim start = pos
        While pos < src.Length AndAlso "+-0123456789.eE".IndexOf(src(pos)) >= 0
            pos += 1
        End While
        If pos = start Then Throw New FormatException("Unexpected character in JSON at " & pos)
        Return Double.Parse(src.Substring(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture)
    End Function

    Private Sub Expect(word As String)
        If String.CompareOrdinal(src, pos, word, 0, word.Length) <> 0 Then
            Throw New FormatException("Expected '" & word & "' in JSON at " & pos)
        End If
        pos += word.Length
    End Sub

    Private Function Peek() As Char
        If pos >= src.Length Then Throw New FormatException("Unexpected end of JSON")
        Return src(pos)
    End Function

    Private Function NextChar() As Char
        Dim c = Peek()
        pos += 1
        Return c
    End Function

    Private Sub SkipWhitespace()
        While pos < src.Length AndAlso Char.IsWhiteSpace(src(pos))
            pos += 1
        End While
    End Sub

End Class
