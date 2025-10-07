10 REM ** Full Feature Test for Microsoft 6502 BASIC **
20 DIM A(3), D(2,2)
30 DIM S$(10), T$(5,5)
40 DEF F(X) = X*X + 2*X - 3
50 REM — DATA / READ / RESTORE
60 RESTORE 1000
70 FOR I = 1 TO 5
80   READ V: PRINT "READ V(", I, ") = "; V
90 NEXT I
100 REM try reading past end
110 ON I GOTO 130, 140, 150
120 GOTO 160
130 PRINT "ON GOTO branch 1"; GOTO 200
140 PRINT "ON GOTO branch 2"; GOTO 200
150 PRINT "ON GOTO branch 3"; GOTO 200
160 PRINT "Unreachable?" ; GOTO 200
200 REM now test ON … GOSUB
210 ON I GOSUB 300, 310, 320
220 GOTO 400
300 PRINT "ON GOSUB -> sub1": RETURN
310 PRINT "ON GOSUB -> sub2": RETURN
320 PRINT "ON GOSUB -> sub3": RETURN
400 REM — INPUT, GET, LET, RND, INT, ABS, SQR, LOG, EXP, SIN, COS
410 RANDOMIZE 1
420 PRINT "Enter a number (X): ";
430 INPUT X
440 LET Y = X * 2.5
450 PRINT "You entered X=", X; " Y = 2.5*X = "; Y
460 PRINT "Some math functions:"
470 PRINT "INT(X)=", INT(X); " ABS(X)=", ABS(X)
480 PRINT "SQR( Y )=", SQR(Y)
490 PRINT "LOG( Y )=", LOG(Y); " EXP(1) =", EXP(1)
500 PRINT "SIN( X )=", SIN(X); " COS( X )=", COS(X)
510 PRINT "Some randoms RND(1):"
520 FOR I = 1 TO 5: PRINT RND(1); : NEXT I
530 PRINT
540 PRINT "Press a key (GET): ";
550 G = GET
560 PRINT "You pressed (ASCII) "; ASC(G)
570 REM — string operations
580 S$ = "HELLO"
590 T$ = "WORLD"
600 PRINT "Concatenate: S$ + T$ = "; S$ + " " + T$
610 PRINT "LEFT$(S$,3) = "; LEFT$(S$,3)
620 PRINT "RIGHT$(T$,3) = "; RIGHT$(T$,3)
630 PRINT "MID$(S$,2,3) = "; MID$(S$,2,3)
640 N = 123.45
650 STRN$ = STR$(N): PRINT "STR$(123.45) = "; STRN$
660 VALN = VAL("678.9"): PRINT "VAL(""678.9"") = "; VALN
670 REM — nested FOR / NEXT
680 PRINT "Nested loops A( ):"
690 FOR I = 1 TO 3
700   FOR J = 1 TO 3
710     A(J) = I * J + 0.5
720     PRINT "A(", I, ",", J, ") = "; A(J)
730   NEXT J
740 NEXT I
750 REM — two-dimensional D( )
760 PRINT "2D D( ):"
770 FOR I = 1 TO 2
780   FOR J = 1 TO 2
790     D(I, J) = I*J + 1
800     PRINT "D(", I, ",", J, ")=", D(I, J)
810   NEXT J
820 NEXT I
830 REM — IF … THEN … ELSE
840 PRINT "Testing IF…THEN…ELSE logic"
850 FOR I = -2 TO 2
860   IF I < 0 THEN PRINT I; " is negative" ELSE IF I = 0 THEN PRINT I; " is zero" ELSE PRINT I; " is positive"
870 NEXT I
880 REM — DEF user-defined function F(X) tested above
890 PRINT "Test F(3) = "; F(3)
900 REM — file I/O (adapt to your dialect)
910 OPEN "TEST.DAT" FOR OUTPUT AS #1
920 PRINT #1, "Line one", 123, "end"
930 PRINT #1, "X=", X; " Y=", Y
940 CLOSE #1
950 OPEN "TEST.DAT" FOR INPUT AS #2
960 INPUT #2, T$, M
970 PRINT "Read from file: "; T$, M
980 CLOSE #2
990 END
1000 REM data lines
1001 DATA 10, 20, 30, 40, 50
