//Buridan paradigm///
//fly plaform v0.1//
//CC BY SA 4.0 //
// first design by Andre Maia Chagas 17/01/2025//
//units in mm //

platD = 117;
platH = 5;
platEdgeD = 146.5;//platD+8;
tolerance = 0.1;
$fn=60;

//make platform
difference(){
cylinder(d=platEdgeD,h=platH,$fn=60);
translate([0,0,3]){
cylinder(d=platD+5,h=2.5);
}//end translate
}//end difference
cylinder(d=platD,h=platH);
%cylinder(d=platEdgeD+22.8,h=platH,$fn=6);