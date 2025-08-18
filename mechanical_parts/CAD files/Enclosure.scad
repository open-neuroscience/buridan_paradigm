use <misc_boards.scad>
use <misc_parts.scad>
use <fillets_and_rounds.scad>

module led_board(){
translate([0,65,14]){rotate([180,0,0]){difference(){
    translate([-1,0,0])union(){
        cube([57,89,1.64]);
        translate([56,0,-10.5])cube([9,86.5,10.5]);
    }
    translate([0,0,-1])linear_extrude(15)raspberrypi_3_model_b_holes();
}}}
translate([1.1,7,12.5]){rotate([180,0,90])pin_female(cols=20,rows=2);}
translate([-1,-25,14])cube([34.5,12.5,12.5]);

}//end module

module cutouts(){
    color("red",0.3)union(){
        translate([-1,-40,14])cube([35,35,13]); // power cables
        translate([50,6,1])cube([15,10.5,3.5]); //usb C
        translate([50,22,1])cube([15,8,3.5]); // mini hdmi
        translate([50,35.5,1])cube([15,8,3.5]); // mini hdmi
        translate([50,53.5,4.2])rotate([0,90,0])cylinder(d=6,h=15,$fn=20); //audio
        translate([50,-22,14])cube([16,87,10.5]); // led output
        translate([1.8,80,1])cube([17,25,14.5]); //ethernet
        translate([21.8,80,0.8])cube([14.5,25,17]); //usb
        translate([39.8,80,0.8])cube([14.5,25,17]); //usb
        translate([-25,7,4])cube([35,52,5]); // gpio knockout
    }}//end module
 
 
module boards(){
    led_board();
    scale(1)board_raspberrypi_4_model_b();
    cutouts();
}

module power_plug(){
    cube([28,70,32.3]);
    translate([33,0,16])rotate([-90,0,0])cylinder(d=3.5,h=10);
    translate([-5,0,16])rotate([-90,0,0])cylinder(d=3.5,h=10);
}//end power_plug

module switch(){
    cube([21.5,25,31]);
}//end switch

module psu(){
    //bottom screws
    translate([6,5,0])union(){
    translate([0,0,0])cylinder(d=3.5,h=10,center=true);
    translate([97,0,0])cylinder(d=3.5,h=10,center=true);
    translate([0,172,0])cylinder(d=3.5,h=10,center=true);
    translate([97,172,0])cylinder(d=3.5,h=10,center=true);
    translate([60,100,0])cylinder(d=3.5,h=10,center=true);
    }//end union
    cube([110,199,50]);
    //side screws
    translate([110,0,50])rotate([0,90,0])union(){
        translate([16.5,6,0])cylinder(d=3.5,h=10,center=true);
        translate([24.5,20,0])cylinder(d=3.5,h=10,center=true);
        translate([12,179,0])cylinder(d=3.5,h=10,center=true);
        translate([37,179,0])cylinder(d=3.5,h=10,center=true);
    }//end union
}//end psu

module stilts(d=6,h=3,fn=6){
    difference(){
        union(){
            translate([3.5,3.5])cylinder(d=d,h=h,$fn=fn);
            translate([52.5,3.5])cylinder(d=d,h=h,$fn=fn);
            translate([3.5,61.5])cylinder(d=d,h=h,$fn=fn);
            translate([52.5,61.5])cylinder(d=d,h=h,$fn=fn);
        }
        linear_extrude(h)raspberrypi_3_model_b_holes();
    }
}//end stilts

module casing(thickness=4){
    translate([0,0,3+thickness])union(){difference(){
        translate([-7-thickness/2,-33-3,-3-thickness])add_rounds(axis="z",R=4,fn=18)cube([65+thickness,94+25+thickness,31+thickness]);
            translate([-2,-28,-3])cube([60,88+25,35]);
            boards();
        translate([0,0,-10])linear_extrude(15)raspberrypi_3_model_b_holes();
        translate([0,0,-7])stilts(d=6,h=3,fn=20);
        
        translate([-5,-32,15])cylinder(d=3.3,h=50,$fn=6);
        translate([55,-32,15])cylinder(d=3.3,h=50,$fn=6);
        translate([-5,82,15])cylinder(d=3.3,h=50,$fn=6);
    }//end difference
    translate([0,0,-3])stilts();
    translate([0,0,-3])stilts(h=1,d=8,fn=20);
}
}//end casing

casing();
//boards();

module casing_lid(thickness=4){
    difference(){
        translate([-7-thickness/2,-33-3])add_rounds(axis="z",R=4,fn=18)cube([65+thickness,94+25+thickness,5]);
        translate([-5,-32])cylinder(d=3.5,h=50,$fn=18);
        translate([55,-32])cylinder(d=3.5,h=50,$fn=18);
        translate([-5,82])cylinder(d=3.5,h=50,$fn=18);
        
        translate([-5,-32,2])cylinder(d=6,h=50,$fn=18);
        translate([55,-32,2])cylinder(d=6,h=50,$fn=18);
        translate([-5,82,2])cylinder(d=6,h=50,$fn=18);
    }//end difference
}//end casing_lid

translate([0,0,36])casing_lid();

module assembly(){
translate([0,0,110])rotate([90,90,0])psu();
translate([-20,30,0])rotate([0,0,180])power_plug();
translate([-30,10,50])rotate([0,0,0])switch();
translate([60,10,50])rotate([90,-90,180])casing();
}//end assembly

//assembly();