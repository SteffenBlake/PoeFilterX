grammar CopiedItem;

document: ITEM_CLASS NL RARITY NL (LINE NL)+ SEPARATOR NL (bodyLine? NL)* EOF;
bodyLine: LINE | SEPARATOR;

ITEM_CLASS: 'Item Class: ' ~[\r\n]+;
RARITY: 'Rarity: ' ~[\r\n]+;
SEPARATOR: '--------';
LINE: ~[\r\n]+;
NL: '\r\n' | '\n' | '\r';
