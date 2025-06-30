use <fillets_and_rounds.scad>

$fn=20;

difference(){
    add_rounds(axis="z"){union(){
        cube([45,15,1.5],center=true);
        translate([0,15+7.5,0])cube([15,30,1.5],center=true);
        }}
    cylinder(d=3.5,h=5,center=true);
    translate([15,0,0])cylinder(d=3.5,h=5,center=true);
    translate([-15,0,0])cylinder(d=3.5,h=5,center=true);
    translate([0,15,0])cylinder(d=3.5,h=5,center=true);
    translate([0,30,0])cylinder(d=3.5,h=5,center=true);
}