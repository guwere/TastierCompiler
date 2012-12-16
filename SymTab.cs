using System;

//Antonio Nikolov 10378197 Tutorial 4
namespace Tastier {

public class Obj {  // object describing a declared name
	public string name;		// name of the object
	public int type;			// type of the object (undef for proc)
	public Obj	next;			// to next object in same scope
	public int kind;      // is the object a var,scope,etc
	public int adr;				// address in memory or start of proc
	public int level;			// nesting level; 0=global, 1=local
	public Obj locals;		// scopes: to locally declared objects
	public int nextAdr;		// scopes: next free address in this scope
	public int mutability; //AN constant or non-constant
    public int dimN;      //number of dimensions of an array object
    public string record_name; // name of the record the object belongs to if it 
                                // was declared in a record
}

public class SymbolTable {
	string [] types = new string [4]  {"Undef  ","Integer","Bool   ","String "};
	string [] kinds = new string [7] {"Var   " ,"Proc  " ,"Scope ","Array","Record","RecVar","RecArr"};
	string [] levels = new string [2] {"Global" ," Local"};
	string [] is_mutable= new string [2] {"Mutable" ," Constant"};

	const int // types
		undef = 0, integer = 1, boolean = 2, str = 3;

	const int // object kinds
		var = 0, proc = 1, scope = 2,arr = 3, record = 4,recvar = 5,recarr = 6;
	const int
		immutable = 1 , mutable = 0;

    
	public int curLevel;	// nesting level of current scope
	public Obj undefObj;	// object node for erroneous symbols
	public Obj topScope;	// topmost procedure scope

	Parser parser;
    int unused;
    public int nextUnused(){return unused++;}

	public SymbolTable(Parser parser) {
		this.parser = parser;
		topScope = null;
		curLevel = -1;
		undefObj = new Obj();
		undefObj.name  =  "undef"; undefObj.type = undef; undefObj.kind = var;
		undefObj.adr = 0; undefObj.level = 0; undefObj.next = null;
	}


	// open a new scope and make it the current scope (topScope)
	public void OpenScope (string name) {
		Obj scop = new Obj();
		scop.name = name ; scop.kind = scope; 
		scop.locals = null; scop.nextAdr = 0;
		scop.next = topScope; topScope = scop; 
		curLevel++;
	
	}


	public void CloseScope () {
		//Console.WriteLine("Closing the Scope of :" + topScope.name); // AN
		Obj temp = topScope.locals;
		topScope = topScope.next; curLevel--;

		//-------------------------------------------AN------------------------------
		while(temp != null){
			Console.WriteLine( temp.name + 
						"\t| " + kinds[temp.kind] + 
						"\t| " + types[temp.type] +  
						"\t| " + levels[temp.level] +
						"\t| " + is_mutable[temp.mutability] + 
						"\t| " + temp.adr);
			temp = temp.next;
		}
		//END-------------------------------------------AN------------------------------
	}
	
	// create a new object node in the current scope
	public Obj NewObj (string name, int kind, int type,string rec) {
		Obj p, last, obj = new Obj();
		obj.name = name; obj.kind = kind; obj.type = type; obj.record_name = rec;
		obj.level = curLevel;
		p = topScope.locals; last = null;
		while (p != null) { 
			if (p.name == name && p.record_name == rec) parser.SemErr("name declared twice");
			last = p; p = p.next;
		}
		if (last == null) topScope.locals = obj; else last.next = obj;
		if (kind == var || kind == arr || kind == recarr || kind == recvar) {
		    obj.adr = topScope.nextAdr++;
		    obj.mutability = mutable; //assume variables are not constant by default
		}
	      return obj;
	}

	//-------------------------------------------AN------------------------------
	public Obj NewConstVar (string name,int kind,string rec){
		Obj obj;
		obj = NewObj (name,kind,undef,rec);
		obj.mutability = immutable;
		return obj;
	}
	//assign the type of constant variable
	public void assignType (Obj obj, int type){
		if( obj.mutability != immutable) parser.SemErr("cannot reasign type of non-constant variable");
		obj.type = type;
	}
	//END-------------------------------------------AN------------------------------
	
	// search the name in all open scopes and return its object node
	public Obj Find (string name,string rec) {
		Obj obj, scope;
		scope = topScope;
		while (scope != null) {  // for all open scopes
			obj = scope.locals;
			while (obj != null) {  // for all objects in this scope
				if (obj.name == name && obj.record_name == rec) return obj;
				obj = obj.next;
			}
			scope = scope.next;
		}
		parser.SemErr(name + " is undeclared");
		return undefObj;
	}

} // end SymbolTable

} // end namespace
