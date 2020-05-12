grammar AlgebraicNotation;

move : moveDesc status?;

moveDesc : ordinaryMoveDesc | KINGSIDE_CASTLE | QUEENSIDE_CASTLE;
ordinaryMoveDesc : pieceKind? source? CAPTURE? destination promotionKind?;

square : FILE RANK;
pieceKind : KNIGHT | BISHOP | ROOK | QUEEN | KING;
source : FILE | RANK | square;
destination : square;
promotionKind : KNIGHT | BISHOP | ROOK | QUEEN;
status : CHECK | CHECKMATE;

KINGSIDE_CASTLE : '0-0' | 'O-O';
QUEENSIDE_CASTLE : '0-0-0' | 'O-O-O';

FILE : 'a'..'h';
RANK : '1'..'8';

KNIGHT : 'N';
BISHOP : 'B';
ROOK : 'R';
QUEEN : 'Q';
KING : 'K';

CAPTURE : 'x';

CHECK : '+';
CHECKMATE : '#';
