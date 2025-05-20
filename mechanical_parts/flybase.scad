//Buridan paradigm///
//fly plaform v0.1//
//CC BY SA 4.0 //
// first design by Andre Maia Chagas 17/01/2025//
//units in mm //

include <makerbeam.scad>

platD = 117;
platH = 5;
platEdgeD = 146.5;//platD+8;
tolerance = 0.1;
$fn=60;
matrixL = 192;//127.7;
matrixH = 192;//127.7;

module platform(){

//make platform
difference(){
cylinder(d=platEdgeD,h=platH,$fn=60);
translate([0,0,3]){
cylinder(d=platD+5,h=2.5);
}//end translate
}//end difference
cylinder(d=platD,h=platH);
//%cylinder(d=platEdgeD+22.8,h=platH,$fn=6);

}//end module

module screens(){
for(i=[0:60:301]){
rotate([0,0,i]){
translate([-matrixL/2,platEdgeD/2+95,0]){
cube([matrixL,12,matrixH]);
        }
    }
/*
*/
}
}//end module

module sticks(){
  for(i=[0:60:301]){  
     //measuring "sticks"
    rotate([0,0,i+90])
translate([170/2,0,10]){
cube([170,10,2],center=true);
}//end translate
  
    }//end for
}//end module

module beams(r=180){
    for(i=[0:60:301]){  
rotate([0,0,i]){
translate([-150/2,r,0]){
color("green")cube([150,15,15]);
}
translate([-150/2,r,165]){
color("green")cube([150,15,15]);
}
rotate([0,90,0]){
translate([-150-15,r,80/2]){
color("blue")cube([150,15,15]);
}
}
rotate([0,90,0]){
translate([-150-15,r,-80/2]){
color("blue")cube([150,15,15]);
}
}
}

        }//end for

        for(i=[0:180:301]){  
rotate([0,0,i+90])
translate([25,-15/2,-0]){
cube([150,15,15]);          
}//end translate

}

    }//end module

module connectors(){
translate([-179/2,155,0]){  
  translate([-40/2,40/2,0])   
color("red")cube([50,20,20]);
rotate([0,0,60]){
translate([-40/2,40/2,0]){
color("red")cube([50,20,20]);
}//end translate

}//end rotate

}//end translate
}//end module


//sticks();
platform();
screens();

translate([0,0,-18]){
beams();
//connectors();
}

//possible join?
//difference(){
//    translate([0,-2.5,-2.5]){rotate([0,0,-30])connectors();}
//    rotate([0,0,-30])beams();}