//vertical beam mounts
$fn=6;
difference(){
    cylinder(d=40,h=15,$fn=6);
    translate([0,-7.5,0])cube([15,15,60],center=true);
    translate([-20,0,0])cube(40);
    
    translate([0,0,7.5])rotate([90,0,0])cylinder(d=3.5,h=20);
    translate([15,0,7.5])rotate([90,0,0])cylinder(d=3.5,h=20);
    translate([-15,0,7.5])rotate([90,0,0])cylinder(d=3.5,h=20);
    
    translate([15,-2,7.5])rotate([90,0,0])cylinder(d=6.5,h=20);
    translate([-15,-2,7.5])rotate([90,0,0])cylinder(d=6.5,h=20);
}//end difference
