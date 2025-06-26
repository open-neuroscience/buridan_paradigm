include <fillets_and_rounds.scad>

module screen_bracket(hole_dist = 34,nut_h = 2,denom=33,r=8,inner=1,outer=1){

difference(){
    scale([1,0.5,1])union(){
scale([hole_dist/denom,1,1])difference(){
    scale([outer,1,1]){rotate([0,0,45]){add_rounds(axis="z",R=10,fn=20)cube([60,60,15],center=true);}}
    
    scale([inner,1,1]){rotate([0,0,45]){
        add_rounds(axis="z",R=4,fn=20)
        cube([42,42,15],center=true);
        }}
    
    translate([-50,0,0])cube(100,center=true);
    translate([51,0,46]){add_rounds(R=r,fn=20,axis="y")cube(100,center=true);}
    
}//end difference
cube([2,76,15],center=true);
translate([-2,0,nut_h]){add_rounds(R=1,fn=6,axis="y")cube([4,76,2],center=true);}
}//end union
translate([hole_dist,0,0])cylinder(h=15,d=3.5,$fn=20,center=true);

offst = 10;
translate([0,offst,nut_h]){rotate([0,90,0])cylinder(d=7,h=10,$fn=6);}
translate([0,-offst,nut_h]){rotate([0,90,0])cylinder(d=7,h=10,$fn=6);}
translate([-5,offst,nut_h]){rotate([0,90,0])cylinder(d=3.5,h=10,$fn=20);}
translate([-5,-offst,nut_h]){rotate([0,90,0])cylinder(d=3.5,h=10,$fn=20);}

translate([-2.5,offst,nut_h])cube([3,6.5,3],center=true);
translate([-2.5,-offst,nut_h])cube([3,6.5,3],center=true);

}
}//end screen bracket

//8mm bracket
screen_bracket(hole_dist=8,inner=0.8,outer=1.2,r=15);

//34mm
//screen_bracket();

//32mm
//screen_bracket(hole_dist=32);

//17mm
//screen_bracket(hole_dist=17,inner=0.9,outer=1.1,r=15);