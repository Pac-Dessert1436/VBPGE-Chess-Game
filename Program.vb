Option Strict On
Imports VbPixelGameEngine

Public NotInheritable Class Program
    Inherits PixelGameEngine

    Public Enum Piece  ' According to the order of sprites in "chess_pieces.png".
        Queen = 0
        Bishop = 1
        Rook = 2
        King = 3
        Knight = 4
        Pawn = 5
        [Nothing] = 6
    End Enum

    Private Enum GameState
        Playing = 0
        Checkmate = 1
        Stalemate = 2
        Promotion = 3
    End Enum

#Region "Game Variables"
    Private ReadOnly Property PieceSize As New Vi2d(12, 16)
    Private ReadOnly Property BoardStartPos As New Vi2d(16, 32)
    Private ReadOnly Property TileSize As New Vi2d(32, 32)  ' Increased for better visibility

    Private ReadOnly m_board(7, 7) As Piece
    Private ReadOnly m_isBlack(7, 7) As Boolean?
    Private ReadOnly sprChessPieces As New Sprite("chess_pieces.png")

    Private m_selectedIdx As New Vi2d(-1, -1)
    Private m_blackTurn As Boolean = False  ' White starts first
    Private m_gameState As GameState = GameState.Playing
    Private m_enPassantTarget As New Vi2d(-1, -1) ' Square where en passant capture is possible
    Private m_lastMoveFrom As New Vi2d(-1, -1)    ' For tracking double pawn moves
    Private m_lastMoveTo As New Vi2d(-1, -1)
    
    ' Castling variables
    Private m_whiteKingMoved As Boolean = False
    Private m_blackKingMoved As Boolean = False
    Private m_whiteRookKingsideMoved As Boolean = False
    Private m_whiteRookQueensideMoved As Boolean = False
    Private m_blackRookKingsideMoved As Boolean = False
    Private m_blackRookQueensideMoved As Boolean = False
#End Region

    Public Sub New()
        AppName = "VBPGE Chess Game"
    End Sub

    Private Sub DrawChessPiece(boardIdx As Vi2d, piece As Piece, isBlack As Boolean)
        If piece = Piece.Nothing Then Exit Sub

        Dim screenPos As New Vi2d(
            BoardStartPos.x + boardIdx.x * TileSize.x + (TileSize.x - PieceSize.x) \ 2,
            BoardStartPos.y + boardIdx.y * TileSize.y + (TileSize.y - PieceSize.y) \ 2
        )
        Dim sprSheetPos As New Vi2d(piece * PieceSize.x, If(isBlack, 16, 0))

        DrawPartialSprite(screenPos, sprChessPieces, sprSheetPos, PieceSize)
    End Sub

    Protected Overrides Function OnUserCreate() As Boolean
        SetPixelMode(Pixel.Mode.Mask)
        ' Clear board
        For y As Integer = 0 To 7
            For x As Integer = 0 To 7
                m_board(x, y) = Piece.Nothing
            Next x
        Next y

        ' Set up black back row
        m_board(0, 0) = Piece.Rook
        m_board(1, 0) = Piece.Knight
        m_board(2, 0) = Piece.Bishop
        m_board(3, 0) = Piece.Queen
        m_board(4, 0) = Piece.King
        m_board(5, 0) = Piece.Bishop
        m_board(6, 0) = Piece.Knight
        m_board(7, 0) = Piece.Rook

        ' Set up black pawns
        For x As Integer = 0 To 7
            m_board(x, 1) = Piece.Pawn
            m_isBlack(x, 1) = True
        Next x

        ' Set up white back row
        m_board(0, 7) = Piece.Rook
        m_board(1, 7) = Piece.Knight
        m_board(2, 7) = Piece.Bishop
        m_board(3, 7) = Piece.Queen
        m_board(4, 7) = Piece.King
        m_board(5, 7) = Piece.Bishop
        m_board(6, 7) = Piece.Knight
        m_board(7, 7) = Piece.Rook

        ' Set up white pawns
        For x As Integer = 0 To 7
            m_board(x, 6) = Piece.Pawn
            m_isBlack(x, 6) = False
        Next x

        ' Set all black back row pieces to black
        For x As Integer = 0 To 7
            m_isBlack(x, 0) = True
            m_isBlack(x, 7) = False
        Next x

        m_gameState = GameState.Playing
        m_blackTurn = False
        m_selectedIdx = New Vi2d(-1, -1)
        m_enPassantTarget = New Vi2d(-1, -1)
        m_lastMoveFrom = New Vi2d(-1, -1)
        m_lastMoveTo = New Vi2d(-1, -1)
        
        ' Reset castling variables
        m_whiteKingMoved = False
        m_blackKingMoved = False
        m_whiteRookKingsideMoved = False
        m_whiteRookQueensideMoved = False
        m_blackRookKingsideMoved = False
        m_blackRookQueensideMoved = False
        Return True
    End Function

    Private Function BoardIdxToScreenPos(boardIdx As Vi2d) As Vi2d
        Return New Vi2d(
            BoardStartPos.x + boardIdx.x * TileSize.x,
            BoardStartPos.y + boardIdx.y * TileSize.y
        )
    End Function

    Private Function ScreenPosToBoardIdx(screenPos As Vi2d) As Vi2d
        Dim boardIdx As New Vi2d(
            (screenPos.x - BoardStartPos.x) \ TileSize.x,
            (screenPos.y - BoardStartPos.y) \ TileSize.y
        )

        ' Clamp to valid board positions
        If boardIdx.x < 0 Then boardIdx.x = 0
        If boardIdx.x > 7 Then boardIdx.x = 7
        If boardIdx.y < 0 Then boardIdx.y = 0
        If boardIdx.y > 7 Then boardIdx.y = 7

        Return boardIdx
    End Function

    Private Function IsValidMove(from As Vi2d, [to] As Vi2d, board As Piece(,)) As Boolean
        Try
            If m_isBlack(from.x, from.y) Is Nothing Then Return False
            ' Can't move to the same position
            If from = [to] Then Return False

            ' Can't move opponent's pieces
            If m_isBlack(from.x, from.y) <> m_blackTurn Then Return False

            ' Can't move to square with own piece
            If board([to].x, [to].y) <> Piece.Nothing AndAlso
                m_isBlack([to].x, [to].y) = m_blackTurn Then Return False
            
            ' Can't capture the opponent's king (chess rule)
            If board([to].x, [to].y) = Piece.King Then Return False

            ' Basic movement rules for each piece type
            Select Case board(from.x, from.y)
                Case Piece.Pawn
                    Return IsValidPawnMove(from, [to], m_board)
                Case Piece.Rook
                    Return IsValidRookMove(from, [to], m_board)
                Case Piece.Knight
                    Return IsValidKnightMove(from, [to])
                Case Piece.Bishop
                    Return IsValidBishopMove(from, [to], m_board)
                Case Piece.Queen
                    Return IsValidRookMove(from, [to], m_board) OrElse
                        IsValidBishopMove(from, [to], m_board)
                Case Piece.King
                    Return IsValidKingMove(from, [to])
                Case Else
                    Return False
            End Select
        Catch ex As IndexOutOfRangeException
            Return False
        End Try
    End Function

#Region "Movement validation functions"
    Private Function IsValidPawnMove(from As Vi2d, [to] As Vi2d, board As Piece(,)) As Boolean
        Dim direction = If(m_blackTurn, 1, -1)
        Dim isFirstMove = If(m_blackTurn, from.y = 1, from.y = 6)

        ' Forward movement
        If from.x = [to].x Then
            ' Single square
            If [to].y = from.y + direction AndAlso
                m_board([to].x, [to].y) = Piece.Nothing Then Return True

            ' Double square (first move only)
            If isFirstMove AndAlso [to].y = from.y + 2 * direction AndAlso
               board(from.x, from.y + direction) = Piece.Nothing AndAlso
               board([to].x, [to].y) = Piece.Nothing Then Return True
        End If

        ' Regular captures (diagonal)
        If Math.Abs(from.x - [to].x) = 1 AndAlso [to].y = from.y + direction AndAlso
           board([to].x, [to].y) <> Piece.Nothing AndAlso
           m_isBlack([to].x, [to].y) <> m_blackTurn Then Return True

        ' En passant capture
        If Math.Abs(from.x - [to].x) = 1 AndAlso [to].y = from.y + direction AndAlso
           board([to].x, [to].y) = Piece.Nothing AndAlso
           [to] = m_enPassantTarget Then Return True

        Return False
    End Function

    Private Shared Function IsValidRookMove(from As Vi2d, [to] As Vi2d, board As Piece(,)) As Boolean
        ' Rooks move horizontally or vertically
        If from.x <> [to].x AndAlso from.y <> [to].y Then Return False

        ' Check if path is clear
        If from.x = [to].x Then
            ' Vertical movement
            Dim start = Math.Min(from.y, [to].y) + 1
            Dim [end] = Math.Max(from.y, [to].y)

            For y As Integer = start To [end] - 1
                If board(from.x, y) <> Piece.Nothing Then Return False
            Next y
        Else
            ' Horizontal movement
            Dim start = Math.Min(from.x, [to].x) + 1
            Dim [end] = Math.Max(from.x, [to].x)

            For x As Integer = start To [end] - 1
                If board(x, from.y) <> Piece.Nothing Then Return False
            Next x
        End If

        Return True
    End Function

    Private Shared Function IsValidKnightMove(from As Vi2d, [to] As Vi2d) As Boolean
        ' Knights move in L-shape: 2 squares in one direction and 1 in the perpendicular
        Dim dx = Math.Abs(from.x - [to].x)
        Dim dy = Math.Abs(from.y - [to].y)

        Return (dx = 2 AndAlso dy = 1) OrElse (dx = 1 AndAlso dy = 2)
    End Function

    Private Shared Function IsValidBishopMove(from As Vi2d, [to] As Vi2d, board As Piece(,)) As Boolean
        ' Bishops move diagonally
        Dim dx = Math.Abs(from.x - [to].x)
        Dim dy = Math.Abs(from.y - [to].y)

        If dx <> dy Then Return False

        ' Check if path is clear
        Dim stepX = If([to].x > from.x, 1, -1)
        Dim stepY = If([to].y > from.y, 1, -1)

        For i As Integer = 1 To dx - 1
            If board(from.x + stepX * i, from.y + stepY * i) <> Piece.Nothing Then Return False
        Next i

        Return True
    End Function

    Private Function IsValidKingMove(from As Vi2d, [to] As Vi2d) As Boolean
        ' Kings move one square in any direction
        Dim dx = Math.Abs(from.x - [to].x)
        Dim dy = Math.Abs(from.y - [to].y)
        
        ' Regular king move
        If dx <= 1 AndAlso dy <= 1 Then Return True
        
        ' Castling kingside
        If Not IsInCheck AndAlso from.y = [to].y AndAlso [to].x = from.x + 2 Then
            ' Check if king has moved
            If (m_blackTurn AndAlso m_blackKingMoved) OrElse (Not m_blackTurn AndAlso m_whiteKingMoved) Then Return False
            
            ' Check if rook has moved
            If m_blackTurn Then
                If m_blackRookKingsideMoved Then Return False
                ' Check if rook exists
                If m_board(7, 0) <> Piece.Rook OrElse m_isBlack(7, 0) <> True Then Return False
            Else
                If m_whiteRookKingsideMoved Then Return False
                ' Check if rook exists
                If m_board(7, 7) <> Piece.Rook OrElse m_isBlack(7, 7) <> False Then Return False
            End If
            
            ' Check if squares between king and rook are empty
            If m_board(from.x + 1, from.y) <> Piece.Nothing OrElse m_board(from.x + 2, from.y) <> Piece.Nothing Then Return False
            
            ' Check if squares the king moves through are not under attack
            Dim tempBoard(7, 7) As Piece
            Dim tempIsBlack(7, 7) As Boolean?
            Array.Copy(m_board, tempBoard, m_board.Length)
            Array.Copy(m_isBlack, tempIsBlack, m_isBlack.Length)
            
            ' Check first square
            tempBoard(from.x, from.y) = Piece.Nothing
            tempIsBlack(from.x, from.y) = Nothing
            tempBoard(from.x + 1, from.y) = Piece.King
            tempIsBlack(from.x + 1, from.y) = m_blackTurn
            
            ' Simulate opponent's turn to check if king is in check
            Dim originalTurn = m_blackTurn
            m_blackTurn = Not m_blackTurn
            Dim isInCheckAfterFirstMove = IsInCheck
            m_blackTurn = originalTurn
            
            If isInCheckAfterFirstMove Then Return False
            
            Return True
        End If
        
        ' Castling queenside
        If Not IsInCheck AndAlso from.y = [to].y AndAlso [to].x = from.x - 2 Then
            ' Check if king has moved
            If (m_blackTurn AndAlso m_blackKingMoved) OrElse (Not m_blackTurn AndAlso m_whiteKingMoved) Then Return False
            
            ' Check if rook has moved
            If m_blackTurn Then
                If m_blackRookQueensideMoved Then Return False
                ' Check if rook exists
                If m_board(0, 0) <> Piece.Rook OrElse m_isBlack(0, 0) <> True Then Return False
            Else
                If m_whiteRookQueensideMoved Then Return False
                ' Check if rook exists
                If m_board(0, 7) <> Piece.Rook OrElse m_isBlack(0, 7) <> False Then Return False
            End If
            
            ' Check if squares between king and rook are empty
            If m_board(from.x - 1, from.y) <> Piece.Nothing OrElse m_board(from.x - 2, from.y) <> Piece.Nothing OrElse m_board(from.x - 3, from.y) <> Piece.Nothing Then Return False
            
            ' Check if squares the king moves through are not under attack
            Dim tempBoard(7, 7) As Piece
            Dim tempIsBlack(7, 7) As Boolean?
            Array.Copy(m_board, tempBoard, m_board.Length)
            Array.Copy(m_isBlack, tempIsBlack, m_isBlack.Length)
            
            ' Check first square
            tempBoard(from.x, from.y) = Piece.Nothing
            tempIsBlack(from.x, from.y) = Nothing
            tempBoard(from.x - 1, from.y) = Piece.King
            tempIsBlack(from.x - 1, from.y) = m_blackTurn
            
            ' Simulate opponent's turn to check if king is in check
            Dim originalTurn = m_blackTurn
            m_blackTurn = Not m_blackTurn
            Dim isInCheckAfterFirstMove = IsInCheck
            m_blackTurn = originalTurn
            
            If isInCheckAfterFirstMove Then Return False
            
            Return True
        End If
        
        Return False
    End Function
#End Region

    Private ReadOnly Property IsInCheck As Boolean
        Get
            Dim kingIdx As New Vi2d(-1, -1)
            For y As Integer = 0 To 7
                For x As Integer = 0 To 7
                    If m_board(x, y) = Piece.King AndAlso m_isBlack(x, y) IsNot Nothing AndAlso
                        m_isBlack(x, y).Value = m_blackTurn Then
                        kingIdx = New Vi2d(x, y)
                        Exit For
                    End If
                Next x
                If kingIdx.x <> -1 Then Exit For
            Next y

            If kingIdx.x = -1 OrElse kingIdx.y = -1 Then
                m_gameState = GameState.Checkmate
                Return False
            End If

            Dim opponentIsBlack As Boolean = Not m_blackTurn
            For atkY As Integer = 0 To 7
                For atkX As Integer = 0 To 7
                    If m_board(atkX, atkY) = Piece.Nothing Then Continue For
                    If m_isBlack(atkX, atkY) Is Nothing OrElse
                        m_isBlack(atkX, atkY).Value <> opponentIsBlack Then Continue For

                    Dim atkIdx As New Vi2d(atkX, atkY)
                    Dim atkPiece As Piece = m_board(atkX, atkY)

                    If atkPiece = Piece.King Then
                        Dim dx As Integer = Math.Abs(atkX - kingIdx.x)
                        Dim dy As Integer = Math.Abs(atkY - kingIdx.y)
                        If dx <= 1 AndAlso dy <= 1 Then Return True
                        Continue For
                    End If

                    If atkPiece = Piece.Pawn Then
                        Dim direction As Integer = If(opponentIsBlack, 1, -1)
                        If (kingIdx.y = atkY + direction) AndAlso (
                            Math.Abs(kingIdx.x - atkX) = 1) Then Return True
                        Continue For
                    End If

                    Dim tempBoard(7, 7) As Piece
                    Dim tempIsBlack(7, 7) As Boolean?
                    Array.Copy(m_board, tempBoard, m_board.Length)
                    Array.Copy(m_isBlack, tempIsBlack, m_isBlack.Length)
                    tempBoard(kingIdx.x, kingIdx.y) = Piece.Nothing
                    tempIsBlack(kingIdx.x, kingIdx.y) = Nothing

                    Select Case atkPiece
                        Case Piece.Rook
                            If IsValidRookMove(atkIdx, kingIdx, tempBoard) Then Return True
                        Case Piece.Knight
                            If IsValidKnightMove(atkIdx, kingIdx) Then Return True
                        Case Piece.Bishop
                            If IsValidBishopMove(atkIdx, kingIdx, tempBoard) Then Return True
                        Case Piece.Queen
                            If IsValidRookMove(atkIdx, kingIdx, tempBoard) OrElse
                                IsValidBishopMove(atkIdx, kingIdx, tempBoard) Then Return True
                    End Select
                Next atkX
            Next atkY

            Return False
        End Get
    End Property

    ' Represents a chess move with from and to positions
    Private Structure ChessMove
        Public from As Vi2d
        Public [to] As Vi2d
        
        Public Sub New(fromPos As Vi2d, toPos As Vi2d)
            Me.from = fromPos
            Me.to = toPos
        End Sub
    End Structure
    
    ' Get all legal moves for the current player
    Private Function GetAllLegalMoves() As List(Of ChessMove)
        Dim legalMoves As New List(Of ChessMove)
        For fromY As Integer = 0 To 7
            For fromX As Integer = 0 To 7
                If m_board(fromX, fromY) = Piece.Nothing Then Continue For
                If m_isBlack(fromX, fromY) Is Nothing OrElse m_isBlack(fromX, fromY).Value <> m_blackTurn Then Continue For
                Dim fromPos As New Vi2d(fromX, fromY)

                For toY As Integer = 0 To 7
                    For toX As Integer = 0 To 7
                        Dim toPos As New Vi2d(toX, toY)

                        ' Skip invalid moves
                        If Not IsValidMove(fromPos, toPos, m_board) Then Continue For

                        ' Simulate the move
                        Dim oriTargetPiece = m_board(toX, toY)
                        Dim oriTargetIsBlack = m_isBlack(toX, toY)
                        Dim oriFromPiece = m_board(fromX, fromY)
                        Dim oriFromIsBlack = m_isBlack(fromX, fromY)

                        ' Handle castling
                        Dim isCastling As Boolean = False
                        Dim castlingRookFrom As Vi2d = Nothing
                        Dim castlingRookTo As Vi2d = Nothing
                        
                        If oriFromPiece = Piece.King Then
                            Dim dx = Math.Abs(fromX - toX)
                            If dx = 2 Then
                                isCastling = True
                                ' Determine rook positions for castling
                                If toX > fromX Then ' Kingside
                                    castlingRookFrom = New Vi2d(7, fromY)
                                    castlingRookTo = New Vi2d(fromX + 1, fromY)
                                Else ' Queenside
                                    castlingRookFrom = New Vi2d(0, fromY)
                                    castlingRookTo = New Vi2d(fromX - 1, fromY)
                                End If
                            End If
                        End If

                        ' Perform the move
                        m_board(toX, toY) = oriFromPiece
                        m_isBlack(toX, toY) = oriFromIsBlack
                        m_board(fromX, fromY) = Piece.Nothing
                        
                        ' Handle castling rook movement
                        If isCastling Then
                            m_board(castlingRookTo.x, castlingRookTo.y) = Piece.Rook
                            m_isBlack(castlingRookTo.x, castlingRookTo.y) = m_blackTurn
                            m_board(castlingRookFrom.x, castlingRookFrom.y) = Piece.Nothing
                        End If

                        ' Check if king is still in check after the move
                        Dim stillInCheck = IsInCheck

                        ' Undo the move
                        m_board(fromX, fromY) = oriFromPiece
                        m_isBlack(fromX, fromY) = oriFromIsBlack
                        m_board(toX, toY) = oriTargetPiece
                        m_isBlack(toX, toY) = oriTargetIsBlack
                        
                        ' Undo castling rook movement
                        If isCastling Then
                            m_board(castlingRookFrom.x, castlingRookFrom.y) = Piece.Rook
                            m_isBlack(castlingRookFrom.x, castlingRookFrom.y) = m_blackTurn
                            m_board(castlingRookTo.x, castlingRookTo.y) = Piece.Nothing
                        End If

                        ' If not in check, add the move to legal moves
                        If Not stillInCheck Then legalMoves.Add(New ChessMove(fromPos, toPos))
                    Next toX
                Next toY
            Next fromX
        Next fromY

        Return legalMoves
    End Function

    Private Sub DetectCheckmate(isInCheck As Boolean)
        Dim allLegalMoves = GetAllLegalMoves()
        Dim hasAnyLegalMove = allLegalMoves.Count > 0
        
        If isInCheck Then
            ' Checkmate if no legal moves to get out of check
            m_gameState = If(hasAnyLegalMove, GameState.Playing, GameState.Checkmate)
        Else
            ' Stalemate if no legal moves and not in check
            m_gameState = If(hasAnyLegalMove, GameState.Playing, GameState.Stalemate)
        End If
        
        ' Additional draw condition: only kings remaining
        Dim nonKingPieces As Integer = 0
        For y As Integer = 0 To 7
            For x As Integer = 0 To 7
                If m_board(x, y) <> Piece.Nothing AndAlso m_board(x, y) <> Piece.King Then
                    nonKingPieces += 1
                End If
            Next x
        Next y
        
        ' If only kings remain, it's a draw (stalemate)
        If nonKingPieces = 0 Then
            m_gameState = GameState.Stalemate
        End If
    End Sub

    Private ReadOnly Property Message As String
        Get
            Dim playerText As String = If(m_blackTurn, "Black", "White")
            Select Case m_gameState
                Case GameState.Playing
                    ' Add "Check!" if current player is in check
                    Return String.Format(
                        "Move pieces with mouse. CURRENT: {0}",
                        If(IsInCheck, $"{playerText} (Check!)", playerText)
                    )
                Case GameState.Checkmate
                    Return String.Format(
                        "Checkmate! Winner: {0} (Press 'R' to restart)",
                        If(Not m_blackTurn, "Black", "White")
                    )
                Case GameState.Stalemate
                    Return "Stalemate! Game Drawn (Press 'R' to restart)"
                Case GameState.Promotion
                    Return $"{playerText} Pawn Promotion - Click a piece to promote"
                Case Else
                    Return String.Empty
            End Select
        End Get
    End Property

#Region "Promotion Menu"
    Private m_promotionPos As New Vi2d(-1, -1) ' Position of pawn to be promoted
    Private ReadOnly m_promotionOptions As New List(Of Piece) From {
        Piece.Queen, Piece.Rook, Piece.Bishop, Piece.Knight
    }
    Private Const PROMO_OPTION_HEIGHT As Integer = 40
    Private Const PROMO_MENU_WIDTH As Integer = 100

    Private Sub HandlePromotionSelection(mousePos As Vi2d)
        Dim menuPos As Vi2d = BoardStartPos + New Vi2d(8 * TileSize.x + 10, 0)

        ' Check if click is within promotion menu
        If mousePos.x >= menuPos.x AndAlso mousePos.x <= menuPos.x + 100 AndAlso
            mousePos.y >= menuPos.y AndAlso mousePos.y <= menuPos.y + PROMO_OPTION_HEIGHT * 4 Then

            ' Determine which option was clicked
            Dim optionIdx As Integer = (mousePos.y - menuPos.y) \ PROMO_OPTION_HEIGHT
            If optionIdx >= 0 AndAlso optionIdx < m_promotionOptions.Count Then
                ' Apply promotion
                m_board(m_promotionPos.x, m_promotionPos.y) = m_promotionOptions(optionIdx)

                ' Return to gameplay
                m_gameState = GameState.Playing
                m_promotionPos = New Vi2d(-1, -1)

                ' Check for checkmate and switch player
                DetectCheckmate(IsInCheck)
                m_blackTurn = Not m_blackTurn
            End If
        End If
    End Sub

    Private Sub DrawPromotionMenu()
        Dim menuPos As Vi2d = BoardStartPos + New Vi2d(8 * TileSize.x + 10, 0)

        ' Draw menu background
        FillRect(menuPos, New Vi2d(PROMO_MENU_WIDTH, PROMO_OPTION_HEIGHT * 4), Presets.DarkGrey)
        DrawRect(menuPos, New Vi2d(PROMO_MENU_WIDTH, PROMO_OPTION_HEIGHT * 4), Presets.White)

        ' Draw promotion options
        For i As Integer = 0 To m_promotionOptions.Count - 1
            Dim promoPiece = m_promotionOptions(i)
            Dim optionPos As New Vi2d(menuPos.x + 10, menuPos.y + i * PROMO_OPTION_HEIGHT + 10)
            DrawString(optionPos, promoPiece.ToString(), Presets.White)
            DrawPartialSprite(
                optionPos + New Vi2d(PROMO_MENU_WIDTH * 2 \ 3, 0),
                sprChessPieces,
                New Vi2d(promoPiece * PieceSize.x, If(m_blackTurn, 16, 0)),
                PieceSize
            )
        Next i
    End Sub
#End Region

    Private Sub HandleGameplayInput(mousePos As Vi2d)
        Dim boardIdx = ScreenPosToBoardIdx(mousePos)

        ' Check if click is within board bounds
        If mousePos.x >= BoardStartPos.x AndAlso
           mousePos.x < BoardStartPos.x + TileSize.x * 8 AndAlso
           mousePos.y >= BoardStartPos.y AndAlso
           mousePos.y < BoardStartPos.y + TileSize.y * 8 Then

            ' If clicking on own piece, select it
            If m_board(boardIdx.x, boardIdx.y) <> Piece.Nothing AndAlso
                m_isBlack(boardIdx.x, boardIdx.y) = m_blackTurn Then
                m_selectedIdx = boardIdx
            ElseIf m_selectedIdx.x <> -1 Then
                ' Try to move piece if something is selected
                If IsValidMove(m_selectedIdx, boardIdx, m_board) Then
                    ' Save move for en passant tracking
                    m_lastMoveFrom = m_selectedIdx
                    m_lastMoveTo = boardIdx

                    ' Handle en passant capture
                    If m_board(m_selectedIdx.x, m_selectedIdx.y) = Piece.Pawn AndAlso
                        boardIdx = m_enPassantTarget Then
                        ' Remove the captured pawn (which is behind the target square)
                        Dim captureY = If(m_blackTurn, boardIdx.y - 1, boardIdx.y + 1)
                        m_board(boardIdx.x, captureY) = Piece.Nothing
                    End If

                    ' Perform the move
                    ' Check if this specific move is legal by getting all legal moves
                    Dim allLegalMoves = GetAllLegalMoves()
                    Dim isMoveLegal = allLegalMoves.Any(Function(move) move.from = m_selectedIdx AndAlso move.to = boardIdx)
                    If Not isMoveLegal Then Exit Sub
                    
                    ' Handle castling
                    If m_board(m_selectedIdx.x, m_selectedIdx.y) = Piece.King Then
                        Dim dx = Math.Abs(m_selectedIdx.x - boardIdx.x)
                        If dx = 2 Then
                            ' Kingside castling
                            If boardIdx.x > m_selectedIdx.x Then
                                ' Move the rook
                                Dim rookFromX = 7
                                Dim rookFromY = If(m_blackTurn, 0, 7)
                                Dim rookToX = m_selectedIdx.x + 1
                                Dim rookToY = m_selectedIdx.y
                                
                                m_board(rookToX, rookToY) = Piece.Rook
                                m_isBlack(rookToX, rookToY) = m_blackTurn
                                m_board(rookFromX, rookFromY) = Piece.Nothing
                            Else ' Queenside castling
                                ' Move the rook
                                Dim rookFromX = 0
                                Dim rookFromY = If(m_blackTurn, 0, 7)
                                Dim rookToX = m_selectedIdx.x - 1
                                Dim rookToY = m_selectedIdx.y
                                
                                m_board(rookToX, rookToY) = Piece.Rook
                                m_isBlack(rookToX, rookToY) = m_blackTurn
                                m_board(rookFromX, rookFromY) = Piece.Nothing
                            End If
                        End If
                        
                        ' Update king moved flag
                        If m_blackTurn Then
                            m_blackKingMoved = True
                        Else
                            m_whiteKingMoved = True
                        End If
                    ElseIf m_board(m_selectedIdx.x, m_selectedIdx.y) = Piece.Rook Then
                        ' Update rook moved flags
                        If m_blackTurn Then
                            If m_selectedIdx.y = 0 Then
                                If m_selectedIdx.x = 0 Then
                                    m_blackRookQueensideMoved = True
                                ElseIf m_selectedIdx.x = 7 Then
                                    m_blackRookKingsideMoved = True
                                End If
                            End If
                        Else
                            If m_selectedIdx.y = 7 Then
                                If m_selectedIdx.x = 0 Then
                                    m_whiteRookQueensideMoved = True
                                ElseIf m_selectedIdx.x = 7 Then
                                    m_whiteRookKingsideMoved = True
                                End If
                            End If
                        End If
                    End If
                    
                    ' Perform the move
                    m_board(boardIdx.x, boardIdx.y) = m_board(m_selectedIdx.x, m_selectedIdx.y)
                    m_isBlack(boardIdx.x, boardIdx.y) = m_isBlack(m_selectedIdx.x, m_selectedIdx.y)
                    m_board(m_selectedIdx.x, m_selectedIdx.y) = Piece.Nothing

                    ' Set en passant target for double pawn moves
                    If m_board(boardIdx.x, boardIdx.y) = Piece.Pawn AndAlso
                        Math.Abs(m_selectedIdx.y - boardIdx.y) = 2 Then
                        ' The square behind the pawn is where en passant can happen
                        m_enPassantTarget = New Vi2d(boardIdx.x, (m_selectedIdx.y + boardIdx.y) \ 2)
                    Else
                        m_enPassantTarget = New Vi2d(-1, -1)
                    End If

                    ' Check for pawn promotion
                    If m_board(boardIdx.x, boardIdx.y) = Piece.Pawn AndAlso
                        (boardIdx.y = 0 OrElse boardIdx.y = 7) Then
                        m_promotionPos = boardIdx
                        m_gameState = GameState.Promotion
                    Else
                        DetectCheckmate(IsInCheck)
                        ' Switch player only if not in promotion state
                        m_blackTurn = Not m_blackTurn
                    End If
                End If

                ' Deselect after attempting move
                m_selectedIdx = New Vi2d(-1, -1)
            End If
        End If
    End Sub

    Protected Overrides Function OnUserUpdate(elapsedTime As Single) As Boolean
        Clear(Presets.DarkBlue)
        DrawString(BoardStartPos - New Vi2d(0, 20), Message, Presets.White)

        If GetKey(Key.R).Pressed Then Call OnUserCreate()
        Dim highlight = Presets.Beige

        ' Draw chess board
        For y As Integer = 0 To 7
            For x As Integer = 0 To 7
                Dim boardIdx As New Vi2d(x, y)
                Dim pos = BoardIdxToScreenPos(boardIdx)
                Dim color = If((x + y) Mod 2 = 0, Presets.Gray, Presets.DarkGrey)

                ' Highlight selected square
                If m_selectedIdx = boardIdx Then color = highlight
                FillRect(pos, TileSize, color)
                DrawRect(pos, TileSize, Presets.Black)
            Next x
        Next y

        ' Draw pieces
        For y As Integer = 0 To 7
            For x As Integer = 0 To 7
                Dim isBlack = m_isBlack(x, y)
                If isBlack Is Nothing Then Continue For
                DrawChessPiece(New Vi2d(x, y), m_board(x, y), isBlack.Value)
            Next x
        Next y

        ' Mark valid moves when a piece is selected
        If m_selectedIdx <> New Vi2d(-1, -1) Then
            ' Get all legal moves for the selected piece
            Dim allLegalMoves = GetAllLegalMoves()
            Dim validMovesForSelected = allLegalMoves.Where(Function(move) move.from = m_selectedIdx)
            
            ' Highlight valid move squares
            For Each move In validMovesForSelected
                ' Calculate the center position of the target tile (for the highlight dot)
                With BoardIdxToScreenPos(move.to)
                    Dim center As New Vi2d(
                        .x + TileSize.x \ 2,  ' Horizontal center of tile
                        .y + TileSize.y \ 2   ' Vertical center of tile
                    )
                    FillCircle(center, 5, highlight)
                End With
            Next move
        End If

        ' Draw promotion menu if active
        If m_gameState = GameState.Promotion Then DrawPromotionMenu()

        ' Handle mouse input
        If GetMouse(0).Pressed Then
            Dim mousePos As New Vi2d(GetMouseX, GetMouseY)

            If m_gameState = GameState.Promotion Then
                ' Handle promotion selection
                HandlePromotionSelection(mousePos)
            ElseIf m_gameState = GameState.Playing Then
                HandleGameplayInput(mousePos)
            End If
        End If

        Return Not GetKey(Key.ESCAPE).Pressed
    End Function

    Friend Shared Sub Main()
        With New Program
            If .Construct(400, 300, fullScreen:=True) Then .Start()
        End With
    End Sub
End Class