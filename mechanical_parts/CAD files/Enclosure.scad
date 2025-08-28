use <misc_boards.scad>
use <misc_parts.scad>
use <fillets_and_rounds.scad>
use <hex_grid.scad>

module power_plug(){
    cube([28,70,32.3]);
    translate([33,0,16])rotate([-90,0,0])cylinder(d=3.5,h=70);
    translate([-5,0,16])rotate([-90,0,0])cylinder(d=3.5,h=70);
}//end power_plug

module switch(){
    cube([21.5,25,31]);
    translate([0,0,1])cube([21.5,30,29]);
}//end switch

module psu(){
    $fn=6;
    //bottom screws
    translate([0,0,0])union(){
    translate([5,7])cylinder(d=3.5,h=50,center=true);
        translate([5,7,-52])cylinder(d=6.6,h=50,center=false,$fn=6);
    translate([92,3])cylinder(d=3.5,h=50,center=true);
        translate([92,3,-52])cylinder(d=6.6,h=50,center=false,$fn=6);
    translate([8.5,61.5])cylinder(d=3.5,h=50,center=true);
        translate([8.5,61.5,-52])cylinder(d=6.6,h=50,center=false);
    translate([88,61.5])cylinder(d=3.5,h=50,center=true);
        translate([88,61.5,-52])cylinder(d=6.6,h=50,center=false);
    translate([8.5,180])cylinder(d=3.5,h=50,center=true);
        translate([8.5,180,-52])cylinder(d=6.6,h=50,center=false);
  translate([88,180])cylinder(d=3.5,h=50,center=true);
        translate([88,180,-52])cylinder(d=6.6,h=50,center=false);
    }//end union
    cube([97.5,199,42.5]);
    //side screws
    translate([30,0,0])union(){
        translate([0,6.5,28.5])rotate([0,90,0])cylinder(h=100,d=3.3);
        translate([0,23.5,21.5])rotate([0,90,0])cylinder(h=100,d=3.3);
        translate([0,178,28.5])rotate([0,90,0])cylinder(h=100,d=3.3);
        translate([0,178,10.5])rotate([0,90,0])cylinder(h=100,d=3.3);
    }//end union
    translate([99,0,0])union(){
        translate([0,6.5,28.5])rotate([0,90,0])cylinder(h=50,d=6.6);
        translate([0,23.5,21.5])rotate([0,90,0])cylinder(h=50,d=6.6);
        translate([0,178,28.5])rotate([0,90,0])cylinder(h=50,d=6.6);
        translate([0,178,10.5])rotate([0,90,0])cylinder(h=50,d=6.6);
    }//end union
}//end psu


module led_board(){
translate([0,65,14]){rotate([180,0,0]){difference(){
    translate([-1,0,0])union(){
        cube([57,89,1.64]);
        translate([56,0,-7.5])cube([9,86.5,10.5]);
    }
    translate([0,0,-1])linear_extrude(15)raspberrypi_3_model_b_holes();
}}}
translate([1.1,7,12.5]){rotate([180,0,90])pin_female(cols=20,rows=2);}
translate([-1,-25,14])cube([34.5,12.5,12.5]);

}//end module

module cutouts(){
    color("red",0.3)union(){
        translate([-2,-40,14])cube([36,35,13]); // power cables
        translate([-2,-31.5,14])cube([36,9,29]); // power cables
        translate([50,6,1])cube([15,10.5,3.5]); //usb C
        translate([50,22,1])cube([15,8,3.5]); // mini hdmi
        translate([50,35.5,1])cube([15,8,3.5]); // mini hdmi
        translate([50,53.5,4.2])rotate([0,90,0])cylinder(d=7,h=15,$fn=20); //audio
        translate([50,-22,11])cube([16,87,10.5]); // led output
        translate([1.8,80,1])cube([17,25,14.5]); //ethernet
        translate([21.8,80,0.8])cube([15.5,25,17]); //usb
        translate([39.8,80,0.8])cube([15.5,25,17]); //usb
        //translate([-25,7,4])cube([35,52,5]); // gpio knockout
        translate([58.5,-10,-2])add_rounds(axis="x",R=4)cube([16,75,10.5]); // thinner wall
    }}//end module
//!cutouts();
    
module boards(){
    led_board();
    scale(1)board_raspberrypi_4_model_b();
    cutouts();
}

module stilts(d=6,h=3,fn=6,holes=true){
    difference(){
        union(){
            translate([3.5,3.5])cylinder(d=d,h=h,$fn=fn);
            translate([52.5,3.5])cylinder(d=d,h=h,$fn=fn);
            translate([3.5,61.5])cylinder(d=d,h=h,$fn=fn);
            translate([52.5,61.5])cylinder(d=d,h=h,$fn=fn);
        }
        if(holes==true){linear_extrude(h)raspberrypi_3_model_b_holes();}
    }
}//end stilts

module casing(thickness=4,screw_head=false,brick=false,left_shift=0){
    
    if(brick==true){//space-filler toggle
        translate([-7-(thickness/2)-left_shift,-33-3,0])cube([65+thickness+left_shift,94+25+thickness,31+thickness]);}
    
    translate([0,0,3+thickness])union(){difference(){
        translate([-7-(thickness/2)-left_shift,-33-3,-3-thickness])
        cube([65+thickness+left_shift,94+25+thickness,31+thickness]);
        
        difference(){
            translate([-2,-28,-3])cube([60,88+25,35]);
            add_rounds(axis="z",R=2)translate([58-8,113-35,23])cube([20,20,10]);// screw holder
        }
            boards();
        translate([0,0,-10])linear_extrude(15)raspberrypi_3_model_b_holes();
        
        //screw from beneath toggle
        if(screw_head==true){
        translate([0,0,-7])stilts(d=6,h=3,fn=20);}
        
        //corner screws
        for(x=[-5-left_shift,55]){
            for(y=[-32,82]){
                    translate([x,y,15])cylinder(d=3.3,h=50,$fn=6);}}
        
        
        difference(){//hex cutouts
            h=10;
            translate([-2,-28,-10])hex_grid(x=60,y=88+25,z=h,d=15,thickness=3);
            translate([0,0,-10])stilts(h=h,d=14,fn=20,holes=false);}//preserve screw hole support
        translate([-2-left_shift,-28,-7])hex_grid(x=left_shift,y=88+25,z=0.4,d=12.5,thickness=3);
        translate([-2-left_shift,-28,35-8])hex_grid(x=left_shift,y=88+25,z=1,d=12.5,thickness=3);
    }//end difference
    translate([0,0,-3])stilts(fn=20);
    translate([0,0,-3])stilts(h=1,d=8,fn=20);
    
}
}//end casing

//casing(left_shift=105-69);


module casing_lid(thickness=4,left_shift=0){
    difference(){
        translate([-7-(thickness/2)-left_shift,-33-3])cube([65+thickness+left_shift,94+25+thickness,5]);
        //corner screws
        for(x=[-5-left_shift,55]){
            for(y=[-32,82]){
                    translate([x,y,0])cylinder(d=3.3,h=50,$fn=6);
                    translate([x,y,2])cylinder(d=6,h=50,$fn=18);}}
        
        translate([-4-left_shift,-30])
                    hex_grid(x=60+thickness+left_shift,y=94+20+thickness,z=0.4,d=14);
    }//end difference
}//end casing_lid

//translate([0,0,36])casing_lid();
//casing_lid(left_shift=105-69);

module assembly(case=false){
    color("blue",0.3){
translate([0,0,110])rotate([90,90,0])psu();
translate([-20,40,30])rotate([0,0,180])power_plug();
translate([-50,14,90])rotate([0,90,0])switch();
if(case==true){
translate([60,0,25])rotate([90,-90,180])casing();}}
}//end assembly

module assembly_cutouts(){
    color("red",0.3)union(){
        translate([-60,-42.5,12.5])add_rounds(axis="x",R=15,fn=4)cube([75,42.5,97.5]);
        translate([-60,0,15])cube([60,35,80]);
        translate([0,12,52.5])add_rounds(axis="x",R=4,fn=20)cube([31.5,20,32]);
        //translate([0,20,25])rotate([0,90,0])cylinder(d=15,h=30);
        *translate([0,20,25])rotate([0,90,0])cylinder(d=3.3,h=50);
        translate([0,20,95])rotate([0,90,0])cylinder(d=3.3,h=50);
        *translate([35.5,20,25])rotate([0,90,0])cylinder(d=6.6,h=5,$fn=6);
        translate([35.5,20,95])rotate([0,90,0])cylinder(d=6.6,h=5,$fn=6);
        
        *translate([-30,15,60])cylinder(d=15,h=100,$fn=6);
        translate([-60,10,60])cube([20,10,100]);
        translate([-45,25,60])cylinder(d=3.3,h=100,$fn=6);
        translate([-45,5,60])cylinder(d=3.3,h=100,$fn=6);
        
        for(y=[-45:81:36]){for(z=[12:96:109]){
            translate([-65,y,z])rotate([0,90,0])cylinder(h=25,d=3.3,$fn=6);
            translate([-65,y,z])rotate([0,90,0])cylinder(h=3.5,d=6,$fn=20);
        }}
    }}//end assembly_cutouts
//!assembly_cutouts();

module power_case(){
    difference(){
        translate([-60,-50,13-5.5])add_rounds(axis="x",R=4,fn=20)cube([91.5,90,105]);
        difference(){
        assembly();
            color("red",0.3)translate([0,-42.5,95])cube(15);
        }
        assembly_cutouts();
    }//end difference
}//end power_case

module power_lid(){
    difference(){
        translate([-65,-50,13-5.5])add_rounds(axis="x",R=4,fn=20)cube([5,90,105]);
        assembly_cutouts();
    }//end difference
}//end power lid

module electronics_case(){
difference(){
translate([67.5,0,52.5])rotate([90,-90,180])casing(left_shift=105-69);
    assembly();
    assembly_cutouts();}}

module electronics_lid(){
intersection(){
translate([67.5,35,52.5])rotate([90,-90,180])casing_lid(left_shift=105-69);
translate([31.5,-50,13-5.5])add_rounds(axis="x",R=4,fn=20)cube([123,90,105]);}}

//assembly();
translate([2,0,0])electronics_case();
translate([2,2,0])electronics_lid();
power_case();
translate([-2,0,0])power_lid();








