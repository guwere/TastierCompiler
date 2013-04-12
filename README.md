cocor_compiler
==============

Use of Coco/R as practical exercise for Compiler Design Theory course

Abbreviations
codegen - Code Generator
symtab - Symbol Table
RT - runtime
CT - compile time
ATG - Tastier.ATG
EBNF - TastierEBNF.txt

Overview of bug fixes, modifications and additions made since last version:

added heap memory to the codegen to handle storage along with the stack.
fixed for loop so it does not ignore the first statement in the loop.
added switch statement that works with int.
reworked strings so they are handled on the heap.
fixed bug where global constants do not get initialized.
added any dimensional array structure with  RT initialization and RT bounds checking that can store int and bool.
added a “Record” structured data type similar to C structs in use that can contain int,bool,string and arrays and constants.
Now there is the ability to write code that is evaluated at RT in the global scope.This to allow for proper constant initialization and record declarations containing arrays. Normal (non-constant) variable and array can be declared in the global scope but not initialized.

Conventions I use :

I refer to single variables int,bool,string as “var” or “variable” where as array as “arr“ or “array” even though array is also a variable.
Above each symtab,codegen procedures and ATG production you can see a comment with:
no initials - no changes made 
AN - small modifications of how the production/procedure works e.g. type.
AN+ - heavy modifications of how the an existing production/procedure works e.g Stat.
AN++ - productions/procedure which I have defined e.g. ArrayDecl.
The EBNF file is up-to-date so refer to it for a simple view of the ATG without the semantic processing
There is extra code printed out when tastier program is interpreted. It is wrapped around curly brackets on the console.


Brief guidelines for the use of the modified/added Tastier language features from a Tastier programmer perspective (refer to EBNF for actual syntax):

Constants: can be declared in any scope including global,local and record scope. Can be initialized with expressions of any type and cannot be changed once assigned. Note that they do not behave like the C style #define macro which just replaces code at compile time.
Switch statement : works only with int expressions.
String type: strings are assigned in the same style as ints and bools.
Arrays: works with int and bool type and can be of any dimensions less than 256 :)          (although did not test with more than four). Can be declared with any integer expression including previously assigned variables and constants in any scope. Example use: 
int i , j;  
i := 4; j := 3;
 array bool myarray[ i ] [ j ] [ i + 3];
myarray[1][2][2] := true;
write myarray[1][2][2];
Record: declared in the global scope only. can contain constants,arrays and any variable type. Variables from the record are accessed by <record_name> “.” <variable_name> and arrays by <record_name> “->” <array_name>.The original record can be used directly or it can be instantiated with another record name by “new” <record_name> <new_record_name>. The names of variables and arrays inside the record do not clash with other global and local names. Example use:
program Tastier{
     const index1 := 3;
     record myrec{
            const index2 := 3;
            int i,j,k;
            string name;
            array int myarr[index1][index2][5];
     }
…........
   void function(){
          int i,j;
          myrec.i := 10;
          i := 11;
         write myrec.i;
         write i + myrec.i;
         new myrec myrec2;
         write myrec2.i // should be print 0
         j := 3;
       write myrec->myarr[1][2][0];
    }


New and modified ATG productions(refer to Tastier.ATG for actual code):

Switch: the condition of the switch is an expression so it leave a value on top of the stack when evaluated. Lets call it “dummy” A new object is created for dummy. The value of dummy  is stored on the stack where the object adr is pointing to. Later, for each “case” expression dummy is loaded after the value of the expression. Equality check and conditional jump are emitted. The address of each conditional jump is saved in variable “caseadr”. At the end of each case statement:  caseadr is patched with the current pc; also if there is a break clause then an unconditional jump is emitted and its address is saved in a List. At the end of the switch statement the all the addresses in the List are patched with the pc that is now just after the switch statement.


VarDecl<int kind,string rec>: kind and rec are inherited attributes where kind tells whether the new object is to be a regular variable or a record variable. rec tells the name of the record it belongs to. Regular variables a part of the empty “” record.


ConstVarDecl<int kind,string rec>: same as above. Also the created object is immutable, and expression is produced and its value assigned(at RT).
ArrayDecl<int kind,string rec>: attribute description same as above except kind can be either regular array or record array. After the ArrayPart is produced a request is made to the codegen to emit an instruction that puts the current heap pointer on the stack. The pointer is stored so this array identified its ident name can be referenced later on. Then the codegen must produce an ALLOCARR instruction.


ArrayPart<out int dim> : synthesizes the number of dimensions. At the end of this production the values of the each dimension will be on the stack with the right most on top. 


Record: produces the complete declaration for a record.


RecordDecl: produces a new copy of the original record so the new copy can be used as a separate record with the same fields as the original.


RecordVarPart<Obj rec, out Obj var>: inherits rec, synthesizes var. Produces a variable ident and checks if it belongs to the inherited record and var syntesizes the value of the variable.


RecordArrPart<Obj rec, out Obj var>: same as above except for arrays.


Factor<out int type>: syntesizes type. Changed so it handles arrays and record fields properly. I feel there is no point for me to go over the selection set of the production.


Stat: same as above.


ProcDecl<out string name>: synthesizes name. No longer sets the progStart. progStart always starts with the first instruction.
Tastier: Changed so now inserts JMP instructions just before each procedure except Main

I have written comments in the codegen and symtab file where I felt there was a need for more thorough explanation of the added opcodes and procedures.

