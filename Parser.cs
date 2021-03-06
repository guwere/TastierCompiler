
using System;

namespace Tastier {



public class Parser {
	public const int _EOF = 0;
	public const int _ident = 1;
	public const int _number = 2;
	public const int _string = 3;
	public const int maxT = 48;

	const bool T = true;
	const bool x = false;
	const int minErrDist = 2;
	
	public Scanner scanner;
	public Errors  errors;

	public Token t;    // last recognized token
	public Token la;   // lookahead token
	int errDist = minErrDist;

const int // types
      undef = 0, integer = 1, boolean = 2,str = 3;

   const int // object kinds
      var = 0, proc = 1 , scope = 2 , arr = 3,record = 4,recvar = 5, recarr = 6;
  const int 
      mutable = 0, immutable = 1;

   public SymbolTable   tab;
   public CodeGenerator gen;
  
/*--------------------------------------------------------------------------*/


	public Parser(Scanner scanner) {
		this.scanner = scanner;
		errors = new Errors();
	}

	void SynErr (int n) {
		if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
		errDist = 0;
	}

	public void SemErr (string msg) {
		if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
		errDist = 0;
	}
	
	void Get () {
		for (;;) {
			t = la;
			la = scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }

			la = t;
		}
	}
	
	void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}
	
	bool StartOf (int s) {
		return set[s, la.kind];
	}
	
	void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}


	bool WeakSeparator(int n, int syFol, int repFol) {
		int kind = la.kind;
		if (kind == n) {Get(); return true;}
		else if (StartOf(repFol)) {return false;}
		else {
			SynErr(n);
			while (!(set[syFol, kind] || set[repFol, kind] || set[0, kind])) {
				Get();
				kind = la.kind;
			}
			return StartOf(syFol);
		}
	}

	
	void AddOp(out Op op) {
		op = Op.ADD; 
		if (la.kind == 4) {
			Get();
		} else if (la.kind == 5) {
			Get();
			op = Op.SUB; 
		} else SynErr(49);
	}

	void ArrayPart(out int dim) {
		int ldim = 0;int type;
		Expect(6);
		Expr(out type);
		Expect(7);
		if(type != integer)SemErr("array index must be of int type"); 
		ldim++; 
		while (la.kind == 6) {
			Get();
			Expr(out type);
			Expect(7);
			if(type != integer)SemErr("array index must be of int type");
			ldim++; 
		}
		dim = ldim; 
	}

	void Expr(out int type) {
		int type1; Op op; 
		SimExpr(out type);
		if (StartOf(1)) {
			RelOp(out op);
			SimExpr(out type1);
			if (type != type1) SemErr("incompatible types");
			gen.Emit(op); type = boolean; 
		}
	}

	void ArrayDecl(int kind,string rec) {
		int type,dim; string name;Obj obj; 
		Expect(8);
		Type(out type);
		if(type != integer 
		 && type != boolean)SemErr("array must be of int or boolean type"); 
		Ident(out name);
		obj = tab.NewObj(name,kind,type,rec); 
		ArrayPart(out dim);
		Expect(9);
		gen.Emit(Op.HPTR); 
		if (obj.level == 0) gen.Emit(Op.STOG, obj.adr);
		    else gen.Emit(Op.STO, tab.curLevel-obj.level, obj.adr); 
		gen.Emit(Op.HPTR);
		gen.Emit(Op.ALLOCARR,dim);  obj.dimN = dim; 
	}

	void Type(out int type) {
		type = undef; 
		if (la.kind == 45) {
			Get();
			type = integer; 
		} else if (la.kind == 46) {
			Get();
			type = boolean; 
		} else if (la.kind == 47) {
			Get();
			type = str; 
		} else SynErr(50);
	}

	void Ident(out string name) {
		Expect(1);
		name = t.val; 
	}

	void ConstVarDecl(int kind,string rec) {
		string name; int type; Obj obj; 
		Expect(10);
		Ident(out name);
		obj = tab.NewConstObj(name,kind,rec);  
		Expect(11);
		Expr(out type);
		tab.assignType(obj,type); 
		   if( type == str) gen.Emit(Op.HPTR);
		      
		      if (obj.level == 0) gen.Emit(Op.STOG, obj.adr);
		         else gen.Emit(Op.STO, tab.curLevel-obj.level, obj.adr);
		      if(type == str){
		          gen.Emit(Op.HPTR);gen.Emit(Op.STRINGW);
		          }
		
		Expect(9);
	}

	void SimExpr(out int type) {
		int type1; Op op; 
		Term(out type);
		while (la.kind == 4 || la.kind == 5) {
			AddOp(out op);
			Term(out type1);
			if (type != integer || type1 != integer) 
			  SemErr("integer type expected");
			gen.Emit(op); 
		}
	}

	void RelOp(out Op op) {
		op = Op.EQU; 
		switch (la.kind) {
		case 25: {
			Get();
			break;
		}
		case 26: {
			Get();
			op = Op.LSS; 
			break;
		}
		case 27: {
			Get();
			op = Op.GTR; 
			break;
		}
		case 28: {
			Get();
			op = Op.NEQ; 
			break;
		}
		case 29: {
			Get();
			op = Op.LSE; 
			break;
		}
		case 30: {
			Get();
			op = Op.GTE; 
			break;
		}
		default: SynErr(51); break;
		}
	}

	void Factor(out int type) {
		int n,dim; Obj obj,obj2; string name; bool rec_access = false;
		type = undef; 
		switch (la.kind) {
		case 1: {
			Ident(out name);
			obj = tab.Find(name,""); type = obj.type;
			if(obj.kind == scope || obj.kind == proc)
			   SemErr("object must be an array or a variable");
			if (la.kind == 6 || la.kind == 22 || la.kind == 23) {
				if (la.kind == 22) {
					RecordVarPart(obj,out obj2);
					obj = obj2; rec_access = true; type = obj.type;
				} else if (la.kind == 6) {
					ArrayPart(out dim);
					if(dim != obj.dimN)SemErr("number of dimensions do not match"); 
				} else {
					RecordArrPart(obj,out obj2);
					obj = obj2; rec_access = true;type = obj2.type;
					ArrayPart(out dim);
					if(dim != obj.dimN)SemErr("number of dimensions do not match"); 
				}
			}
			if((obj.kind == recvar || obj.kind == recarr) && !rec_access)
			 SemErr("object belongs to a record. Maybe prepend the record name?"); 
			if (obj.level == 0) gen.Emit(Op.LOADG, obj.adr);
			   else gen.Emit(Op.LOAD, tab.curLevel-obj.level, obj.adr); 
			if(obj.kind == recarr || obj.kind == arr) gen.Emit(Op.ACCARR);
			if( obj.type == str) gen.Emit(Op.STRINGR);
			
			break;
		}
		case 2: {
			Get();
			n = Convert.ToInt32(t.val); 
			gen.Emit(Op.CONST, n); type = integer; 
			break;
		}
		case 5: {
			Get();
			Factor(out type);
			if (type != integer) {
			  SemErr("integer type expected"); type = integer;
			}
			gen.Emit(Op.NEG); 
			break;
		}
		case 12: {
			Get();
			gen.Emit(Op.CONST, 1); type = boolean; 
			break;
		}
		case 13: {
			Get();
			gen.Emit(Op.CONST, 0); type = boolean; 
			break;
		}
		case 3: {
			Get();
			for(int ch = t.val.Length-1; ch >= 1; ch--){
			           gen.Emit(Op.CONST,t.val[ch]);
			       }
			       gen.Emit(Op.CONST,t.val.Length-2);
			       type = str; 
			break;
		}
		default: SynErr(52); break;
		}
	}

	void RecordVarPart(Obj rec,out Obj var) {
		string name;
		Expect(22);
		if(rec.kind != record) SemErr("LHS of the dot must be a record name"); 
		Ident(out name);
		var = tab.Find(name,rec.name); 
		if(var.kind != recvar)
		  SemErr("RHS of the dot must be a var belonging to the record on the LHS");
		if(var.record_name != rec.name)
		  SemErr("var RHS of the dot must belong to the record on the LHS"); 
	}

	void RecordArrPart(Obj rec,out Obj arr) {
		string name;
		Expect(23);
		if(rec.kind != record) SemErr("LHS of the dot must be a record name"); 
		Ident(out name);
		arr = tab.Find(name,rec.name); 
		if(arr.kind != recarr)
		  SemErr("RHS of the dot must be an arrya belonging to the record on the LHS");
		if(arr.record_name != rec.name)
		  SemErr("array RHS of the dot must belong to the record on the LHS"); 
	}

	void MulOp(out Op op) {
		op = Op.MUL; 
		if (la.kind == 14) {
			Get();
		} else if (la.kind == 15) {
			Get();
			op = Op.DIV; 
		} else SynErr(53);
	}

	void ProcDecl(out string name) {
		Obj obj; int adr, adr2;string dontcare; 
		Expect(16);
		Ident(out name);
		obj = tab.NewObj(name, proc, undef,""); obj.adr = gen.pc;
		if (name == "Main") {                           // 
		  obj.level= 0;         // 
		}                                               // 
		  else obj.level = tab.curLevel+1;             // 
		tab.OpenScope(name); 
		Expect(17);
		Expect(18);
		Expect(19);
		gen.Emit(Op.ENTER, 0); adr = gen.pc - 2; 
		while (StartOf(2)) {
			switch (la.kind) {
			case 24: {
				RecordDecl();
				break;
			}
			case 10: {
				ConstVarDecl(var,"");
				break;
			}
			case 45: case 46: case 47: {
				VarDecl(var,"");
				break;
			}
			case 1: case 19: case 31: case 37: case 39: case 40: case 41: case 42: {
				Stat();
				break;
			}
			case 8: {
				ArrayDecl(arr,"");
				break;
			}
			case 16: {
				gen.Emit(Op.JMP, 0); adr2 = gen.pc - 2; 
				ProcDecl(out dontcare);
				gen.Patch(adr2, gen.pc); 
				break;
			}
			}
		}
		Expect(20);
		gen.Emit(Op.LEAVE); gen.Emit(Op.RET);
		gen.Patch(adr, tab.topScope.nextAdr);
		
		Console.WriteLine("name\t|kind\t|type\t|lev\t|mut\t|addr");
		Console.WriteLine("----------------------------------------------\n");
		tab.CloseScope(); 
	}

	void RecordDecl() {
		string name,name2; 
		Expect(24);
		Ident(out name);
		Ident(out name2);
		if(name == name2)
		   SemErr("name of record variable must be different from the original");
		tab.NewRecord(name,name2);
		
		Expect(9);
	}

	void VarDecl(int kind,string rec) {
		string name; int type; 
		Type(out type);
		Ident(out name);
		tab.NewObj(name, kind, type,rec); 
		while (la.kind == 43) {
			Get();
			Ident(out name);
			tab.NewObj(name, kind, type,rec); 
		}
		Expect(9);
	}

	void Stat() {
		int type,type1,type2; string name; Obj obj,obj2;
		int adr, adr2, loopstart,assg_start; int dim;
		bool rec_access = false;
		switch (la.kind) {
		case 1: {
			Ident(out name);
			obj = tab.Find(name,""); 
			if (la.kind == 6 || la.kind == 23) {
				if (la.kind == 23) {
					RecordArrPart(obj,out obj2);
					obj = obj2; rec_access = true;type = obj2.type;
				}
				if(obj.kind == recarr && !rec_access)
				  SemErr("object belongs to a record. Maybe prepend the record name?"); 
				ArrayPart(out dim);
				if(dim != obj.dimN)SemErr("number of dimensions do not match"); 
				Expect(11);
				Expr(out type);
				if (obj.level == 0) gen.Emit(Op.LOADG, obj.adr);
				   else gen.Emit(Op.LOAD, tab.curLevel-obj.level, obj.adr); 
				gen.Emit(Op.ASSGARR); 
				Expect(9);
			} else if (la.kind == 11 || la.kind == 22) {
				if (la.kind == 22) {
					RecordVarPart(obj,out obj2);
					obj = obj2; rec_access = true; type = obj.type;
				}
				if(obj.kind == recvar && !rec_access)
				SemErr("object belongs to a record. Maybe prepend the record name?"); 
				Expect(11);
				if(obj.mutability == immutable)SemErr("cannot reasign constant object" ); 
				Expr(out type);
				if (la.kind == 9) {
					Get();
					if(type != obj.type) SemErr("types do not match");     
					
					if( type == str) gen.Emit(Op.HPTR);
					
					if (obj.level == 0) gen.Emit(Op.STOG, obj.adr);
					 else gen.Emit(Op.STO, tab.curLevel-obj.level, obj.adr);
					if(type == str){
					  gen.Emit(Op.HPTR);gen.Emit(Op.STRINGW);
					}
				} else if (la.kind == 36) {
					Get();
					if(type != boolean) SemErr("Expecting boolean condition");
					gen.Emit(Op.FJMP,0); adr = gen.pc - 2;
					Expr(out type1);
					if(obj.type != type1) SemErr("incompatible types2");
					gen.Emit(Op.JMP,0); gen.Patch(adr,gen.pc);adr = gen.pc - 2;
					if (type1 != obj.type) SemErr("incompatible types3");
					if (obj.level == 0) gen.Emit(Op.STOG, obj.adr);
					else gen.Emit(Op.STO, tab.curLevel-obj.level, obj.adr); 
					Expect(33);
					Expr(out type2);
					if(obj.type != type2) SemErr("incompatible types4");
					gen.Patch(adr,gen.pc);
					if (type2 != obj.type) SemErr("incompatible types5");
					if (obj.level == 0) gen.Emit(Op.STOG, obj.adr);
					else gen.Emit(Op.STO, tab.curLevel-obj.level, obj.adr); 
					Expect(9);
				} else SynErr(54);
			} else if (la.kind == 17) {
				Get();
				Expect(18);
				Expect(9);
				if (obj.kind != proc) SemErr("object is not a procedure");
				  gen.Emit(Op.CALL, obj.level-tab.curLevel, obj.adr); 
			} else SynErr(55);
			break;
		}
		case 37: {
			Get();
			Expect(17);
			Expr(out type);
			Expect(18);
			if (type != boolean) SemErr("boolean type expected");
			  gen.Emit(Op.FJMP, 0); adr = gen.pc - 2; 
			Stat();
			if (la.kind == 38) {
				Get();
				gen.Emit(Op.JMP, 0); adr2 = gen.pc - 2;
				gen.Patch(adr, gen.pc); adr = adr2; 
				Stat();
			}
			gen.Patch(adr, gen.pc); 
			break;
		}
		case 39: {
			Get();
			loopstart = gen.pc; 
			Expect(17);
			Expr(out type);
			Expect(18);
			if (type != boolean) SemErr("boolean type expected");
			  gen.Emit(Op.FJMP, 0); adr = gen.pc - 2; 
			Stat();
			gen.Emit(Op.JMP, loopstart); gen.Patch(adr, gen.pc); 
			break;
		}
		case 40: {
			Get();
			Expect(17);
			Ident(out name);
			obj = tab.Find(name,""); 
			Expect(11);
			if(obj.mutability == immutable)SemErr("cannot reasign constant object" ); 
			Expr(out type);
			Expect(9);
			if(type != obj.type) SemErr("incompatible types");
			    if (obj.level == 0) gen.Emit(Op.STOG, obj.adr);
			    else gen.Emit(Op.STO, tab.curLevel-obj.level, obj.adr); 
			       gen.Emit(Op.JMP,0);assg_start = gen.pc-2; loopstart = gen.pc;
			Ident(out name);
			obj2 = tab.Find(name,""); 
			Expect(11);
			if(obj2.mutability == immutable)SemErr("cannot reasign constant object" ); 
			Expr(out type1);
			Expect(9);
			if(type1 != obj2.type) SemErr("incompatible types");
			   if (obj.level == 0) gen.Emit(Op.STOG, obj.adr);
			   else gen.Emit(Op.STO, tab.curLevel-obj.level, obj.adr);
			   gen.Patch(assg_start,gen.pc);
			Expr(out type2);
			if(type2 != boolean) SemErr("expecting boolean conditional");
			 gen.Emit(Op.FJMP,0); adr = gen.pc - 2;
			Expect(18);
			Expect(19);
			Stat();
			gen.Emit(Op.JMP,loopstart); gen.Patch(adr,gen.pc); 
			Expect(20);
			break;
		}
		case 31: {
			Switch();
			break;
		}
		case 41: {
			Get();
			Ident(out name);
			Expect(9);
			obj = tab.Find(name,"");
			if (obj.type != integer) SemErr("integer type expected");
			  gen.Emit(Op.READ);
			if (obj.level == 0) gen.Emit(Op.STOG, obj.adr);
			//                             else gen.Emit(Op.STO, obj.adr); .)                            ***
			  else gen.Emit(Op.STO, tab.curLevel-obj.level, obj.adr); 
			break;
		}
		case 42: {
			Get();
			Expr(out type);
			if(type == str) gen.Emit(Op.SWRITE);					//AN
			else if(type == integer) gen.Emit(Op.WRITE);		//AN
			else SemErr("expected type int or string"); 
			while (la.kind == 43) {
				Get();
				Expr(out type);
				if(type == str) gen.Emit(Op.SWRITE); 					//AN
				 else if(type == integer) gen.Emit(Op.WRITE);		//AN
				else SemErr("expected type int or string"); 
			}
			Expect(9);
			gen.Emit(Op.NEWLINE);
			break;
		}
		case 19: {
			Get();
			while (StartOf(3)) {
				if (StartOf(4)) {
					Stat();
				} else if (la.kind == 24) {
					RecordDecl();
				} else if (la.kind == 45 || la.kind == 46 || la.kind == 47) {
					VarDecl(var,"");
				} else if (la.kind == 10) {
					ConstVarDecl(var,"");
				} else {
					ArrayDecl(arr,"");
				}
			}
			Expect(20);
			break;
		}
		default: SynErr(56); break;
		}
	}

	void Record() {
		string name;  
		Expect(21);
		Ident(out name);
		tab.NewObj(name,record,undef,""); 
		Expect(19);
		while (StartOf(5)) {
			if (la.kind == 45 || la.kind == 46 || la.kind == 47) {
				VarDecl(recvar,name);
			} else if (la.kind == 8) {
				ArrayDecl(recarr,name);
			} else {
				ConstVarDecl(recvar,name);
			}
		}
		Expect(20);
	}

	void Term(out int type) {
		int type1; Op op; 
		Factor(out type);
		while (la.kind == 14 || la.kind == 15) {
			MulOp(out op);
			Factor(out type1);
			if (type != integer || type1 != integer) 
			  SemErr("integer type expected");
			gen.Emit(op); 
		}
	}

	void Switch() {
		int type,type1,caseaddr;
		 Obj dummy = tab.NewObj("sw" + tab.nextUnused(),0,undef,"");
		 System.Collections.Generic.List<int> breakaddrs
		 = new System.Collections.Generic.List<int>();
		Expect(31);
		Expect(17);
		Expr(out type);
		Expect(18);
		if(type != integer) SemErr("must have int type in case expr");
		gen.Emit(Op.STO, tab.curLevel-dummy.level, dummy.adr);
		Expect(19);
		while (la.kind == 32) {
			Get();
			Expr(out type1);
			if(type1 != type) SemErr("case type must match switch type");
			gen.Emit(Op.LOAD, tab.curLevel-dummy.level, dummy.adr); 
			gen.Emit(Op.EQU); gen.Emit(Op.FJMP,0);caseaddr = gen.pc - 2; 
			Expect(33);
			if (StartOf(4)) {
				Stat();
			}
			if (la.kind == 34) {
				Get();
				gen.Emit(Op.JMP,0); breakaddrs.Add(gen.pc-2);
				
			}
			gen.Patch(caseaddr,gen.pc); 
		}
		if (la.kind == 35) {
			Get();
			Expect(33);
			if (StartOf(4)) {
				Stat();
			}
			if (la.kind == 34) {
				Get();
				gen.Emit(Op.JMP,0); breakaddrs.Add(gen.pc-2); 
			}
		}
		Expect(20);
		foreach(int addr in breakaddrs) gen.Patch(addr,gen.pc); 
	}

	void Tastier() {
		string name;int adr;bool main_declared = false; 
		Expect(44);
		Ident(out name);
		tab.OpenScope(name); 
		Expect(19);
		while (StartOf(6)) {
			switch (la.kind) {
			case 21: {
				Record();
				break;
			}
			case 10: {
				ConstVarDecl(var,"");
				break;
			}
			case 45: case 46: case 47: {
				VarDecl(var,"");
				break;
			}
			case 24: {
				RecordDecl();
				break;
			}
			case 8: {
				ArrayDecl(arr,"");
				break;
			}
			case 16: {
				adr = gen.pc; gen.Emit(Op.NOP);gen.Emit(Op.NOP);gen.Emit(Op.NOP);
				ProcDecl(out name);
				if(name != "Main") gen.PatchInstruction(adr,Op.JMP,gen.pc);
				else main_declared  = true;
				
				break;
			}
			}
		}
		Expect(20);
		tab.CloseScope();
		if (!main_declared) SemErr("main function never defined");
		
	}



	public void Parse() {
		la = new Token();
		la.val = "";		
		Get();
		Tastier();
		Expect(0);

	}
	
	static readonly bool[,] set = {
		{T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,T,x,x, x,x,x,x, T,x,T,x, x,x,x,x, T,x,x,T, x,x,x,x, T,x,x,x, x,x,x,T, x,x,x,x, x,T,x,T, T,T,T,x, x,T,T,T, x,x},
		{x,T,x,x, x,x,x,x, T,x,T,x, x,x,x,x, x,x,x,T, x,x,x,x, T,x,x,x, x,x,x,T, x,x,x,x, x,T,x,T, T,T,T,x, x,T,T,T, x,x},
		{x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,T,x,T, T,T,T,x, x,x,x,x, x,x},
		{x,x,x,x, x,x,x,x, T,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,T, x,x},
		{x,x,x,x, x,x,x,x, T,x,T,x, x,x,x,x, T,x,x,x, x,T,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,T, x,x}

	};
} // end Parser


public class Errors {
	public int count = 0;                                    // number of errors detected
	public System.IO.TextWriter errorStream = Console.Out;   // error messages go to this stream
	public string errMsgFormat = "-- line {0} col {1}: {2}"; // 0=line, 1=column, 2=text

	public virtual void SynErr (int line, int col, int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "ident expected"; break;
			case 2: s = "number expected"; break;
			case 3: s = "string expected"; break;
			case 4: s = "\"+\" expected"; break;
			case 5: s = "\"-\" expected"; break;
			case 6: s = "\"[\" expected"; break;
			case 7: s = "\"]\" expected"; break;
			case 8: s = "\"array\" expected"; break;
			case 9: s = "\";\" expected"; break;
			case 10: s = "\"const\" expected"; break;
			case 11: s = "\":=\" expected"; break;
			case 12: s = "\"true\" expected"; break;
			case 13: s = "\"false\" expected"; break;
			case 14: s = "\"*\" expected"; break;
			case 15: s = "\"/\" expected"; break;
			case 16: s = "\"void\" expected"; break;
			case 17: s = "\"(\" expected"; break;
			case 18: s = "\")\" expected"; break;
			case 19: s = "\"{\" expected"; break;
			case 20: s = "\"}\" expected"; break;
			case 21: s = "\"record\" expected"; break;
			case 22: s = "\".\" expected"; break;
			case 23: s = "\"->\" expected"; break;
			case 24: s = "\"new\" expected"; break;
			case 25: s = "\"=\" expected"; break;
			case 26: s = "\"<\" expected"; break;
			case 27: s = "\">\" expected"; break;
			case 28: s = "\"!=\" expected"; break;
			case 29: s = "\"<=\" expected"; break;
			case 30: s = "\">=\" expected"; break;
			case 31: s = "\"switch\" expected"; break;
			case 32: s = "\"case\" expected"; break;
			case 33: s = "\":\" expected"; break;
			case 34: s = "\"break\" expected"; break;
			case 35: s = "\"default\" expected"; break;
			case 36: s = "\"?\" expected"; break;
			case 37: s = "\"if\" expected"; break;
			case 38: s = "\"else\" expected"; break;
			case 39: s = "\"while\" expected"; break;
			case 40: s = "\"for\" expected"; break;
			case 41: s = "\"read\" expected"; break;
			case 42: s = "\"write\" expected"; break;
			case 43: s = "\",\" expected"; break;
			case 44: s = "\"program\" expected"; break;
			case 45: s = "\"int\" expected"; break;
			case 46: s = "\"bool\" expected"; break;
			case 47: s = "\"string\" expected"; break;
			case 48: s = "??? expected"; break;
			case 49: s = "invalid AddOp"; break;
			case 50: s = "invalid Type"; break;
			case 51: s = "invalid RelOp"; break;
			case 52: s = "invalid Factor"; break;
			case 53: s = "invalid MulOp"; break;
			case 54: s = "invalid Stat"; break;
			case 55: s = "invalid Stat"; break;
			case 56: s = "invalid Stat"; break;

			default: s = "error " + n; break;
		}
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}

	public virtual void SemErr (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}
	
	public virtual void SemErr (string s) {
		errorStream.WriteLine(s);
		count++;
	}
	
	public virtual void Warning (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
	}
	
	public virtual void Warning(string s) {
		errorStream.WriteLine(s);
	}
} // Errors


public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}
}