//flybase

module platform(
plat_d=117,
h=5,
moat_w=60,
outer_d=250,
fn=60){
    $fn = fn;
    difference(){
        cylinder(h=h,d=outer_d);
        translate([0,0,2])cylinder(h=h,d=plat_d+moat_w*2);
    }// difference
    cylinder(h=h,d=plat_d);
}// platform

platform();