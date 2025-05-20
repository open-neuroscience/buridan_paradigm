use <flybase.scad>

module green_beams(w=15,l=150,r=180){
    for(i=[0:60:301]){  
rotate([0,0,i]){
translate([-l/2,r,0]){
color("green")cube([l,w,w]);
}}}}

module side_holes(w=3.3,l=60,r=180){
    for(i=[0:60:301]){  
rotate([0,0,i]){
translate([60,r,0]){
rotate([90,0,0])cylinder(h=l,d=w,$fn=6,center=true);}
    translate([-60,r,0]){
rotate([90,0,0])cylinder(h=l,d=w,$fn=6,center=true);
}}}}


difference(){
    translate([0,0,-5])green_beams(w=20,l=225,r=177.5);
    green_beams(w=15.5);
    translate([0,0,-10])green_beams(w=50,l=70,r=175);
    translate([0,0,7.5])side_holes();
}//end difference

