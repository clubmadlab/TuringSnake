
Instructions
============

< - move one square to the left
<n - move n squares to the left
<< - move to the first square

> - move one square to the right
>n - move n squares right
>> - move to the last square

can move off tape but only by one square

$variable=expression - assign value to variable
variable names in lower case, digits and _
can start with a digit
10 significant characters
10 total number of variables
$x, $count, $1, $y_2 are all valid
variables are 8-bit in the range -128 to 127
variables wrap at these limits rather than saturate

$variable++ - increment variable
$variable-- - decrement variable

?expression action - conditional test for non-zero
?!expression action - conditional test for zero
?>expression action - conditional test for greater than
?>=expression action - conditional test for greater than or equal
?<expression action - conditional test for less than
?<=expression action - conditional test for less than or equal

#label - define a label
label names in lower case, digits and _
can start with a digit
10 significant characters
#start, #end, #lbl1, #2 are all valid

^label - branch to label

%n - wait for n periods
% - wait for a single period
%0 - halt

if 'Repeat step until wait' enabled program must include a %
ESC to break from a run-away program

R - change current square to red
G - change current square to green
B - change current square to blue
C - change current square to cyan
M - change current square to magenta
Y - change current square to yellow
W - change current square to white
K - change current square to black (clear square)

; comment until end of line
