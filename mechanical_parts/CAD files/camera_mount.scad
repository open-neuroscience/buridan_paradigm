//camera mount

use <fillets_and_rounds.scad>

module camera_mount(){
    x_offset = 15.5;
    y_offset = 7.5;
    
    difference(){
    union(){
        add_rounds(axis="z",R=3,fn=20)cube([47,30,5],center=true);
        translate([0,0,-7.5])add_rounds(axis="z",R=3,fn=20)cube([47,30,15],center=true);
    }//end union
    for(i=[-1:2:1]){
        for(j = [-1:2:1]){
            translate([i*x_offset,j*y_offset,0])cylinder(d=3.5,h=50,center=true);
            //translate([i*x_offset,j*y_offset,-1])cylinder(d=6.5,h=3.5,$fn=6);
        }//end for
    }//end for
}
    
}//end module

module frame_mount(){
    difference(){
    translate([0,8,10])camera_mount();
    cube([60,15,15],center=true);
    rotate([90,0,0])cylinder(d=3.5,h=50,center=true);
        translate([0,15+5.5,0])rotate([90,0,0])cylinder(d=6.6,h=15,$fn=6,center=true);
    }//end difference
}//end frame_mount

frame_mount();