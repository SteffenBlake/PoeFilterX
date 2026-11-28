grammar FilterX;

document: statement* EOF;
statement: packageStatement | importStatement | block | command;
packageStatement: IMPORT argument FROM argument (AT argument)? SEMICOLON;
importStatement: USING argument SEMICOLON;
block: OPEN_BRACE statement* CLOSE_BRACE | OPEN_BRACKET statement* CLOSE_BRACKET;
command: ATOM argument* SEMICOLON;
argument: ATOM | STRING | USING | IMPORT | FROM | AT | OPERATOR;

IMPORT: [iI][mM][pP][oO][rR][tT];
FROM: [fF][rR][oO][mM];
AT: [aA][tT];
USING: [uU][sS][iI][nN][gG];
OPEN_BRACE: '{';
CLOSE_BRACE: '}';
OPEN_BRACKET: '[';
CLOSE_BRACKET: ']';
SEMICOLON: ';';
OPERATOR: '!=' | '==' | '>=' | '<=' | '=' | '>' | '<' | '!';
STRING: '"' ('\\' ["\\nt] | ~["\\])* '"';
// These tokens preserve malformed literals for precise diagnostics and editor recovery.
UNTERMINATED_STRING: '"' ('\\' . | ~["\\])* '\\'? EOF;
INVALID_STRING: '"' ('\\' . | ~["\\])* '"';
COMMENT: '#' ~[\r\n]* -> channel(HIDDEN);
WHITESPACE: [ \t\r\n\uFEFF]+ -> channel(HIDDEN);
ATOM: ~[ \t\r\n\uFEFF{}\u005B\u005D;"#\\=<>!]+;
INVALID: .;
