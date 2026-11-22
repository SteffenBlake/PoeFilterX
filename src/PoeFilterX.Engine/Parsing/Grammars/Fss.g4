grammar Fss;

document: statement* EOF;
statement: importStatement | style;
importStatement: USING argument SEMICOLON;
style: ATOM OPEN_BRACE declaration* CLOSE_BRACE;
declaration: ATOM COLON argument* SEMICOLON;
argument: ATOM | STRING | USING;

USING: [uU][sS][iI][nN][gG];
OPEN_BRACE: '{';
CLOSE_BRACE: '}';
COLON: ':';
SEMICOLON: ';';
STRING: '"' ('\\' ["\\nt] | ~["\\])* '"';
// These tokens preserve malformed literals for precise diagnostics and editor recovery.
UNTERMINATED_STRING: '"' ('\\' . | ~["\\])* '\\'? EOF;
INVALID_STRING: '"' ('\\' . | ~["\\])* '"';
COMMENT: '/*' .*? ('*/' | EOF) -> channel(HIDDEN);
WHITESPACE: [ \t\r\n\uFEFF]+ -> channel(HIDDEN);
// A slash may occur in unquoted paths, but must not swallow a comment opener.
ATOM: (~[ \t\r\n\uFEFF{}:;"\\/] | '/' ~[* \t\r\n{}:;"\\])* ~[ \t\r\n\uFEFF{}:;"\\/]
    | '/' ;
INVALID: .;
