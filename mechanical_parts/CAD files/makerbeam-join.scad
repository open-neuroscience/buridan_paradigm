use <flybase.scad>
use <fillets_and_rounds.scad>

module green_beams(w=15,h=15,l=150,r=180){
    if(h==0){h=w;}
    for(i=[0:60:301]){  
rotate([0,0,i]){
translate([-l/2,r,0]){
color("green")cube([l,w,h]);
}}}}

module side_holes(w=3.3,l=60,r=180, n=25, spacing=1,fn=6,buffer=40){
    for(i=[0:60:301]){  
rotate([0,0,i]){
    for(k=[0:spacing:n]){
translate([buffer+k,r,0]){
rotate([90,0,0])cylinder(h=l,d=w,$fn=fn,center=true);}
    translate([-buffer-k,r,0]){
rotate([90,0,0])cylinder(h=l,d=w,$fn=fn,center=true);}
}}}}

module columns(d,h,n,r,fn=6,center=true){
    for(i=[1:1:n]){
        rotate([0,0,i*360/n]){translate([r,0,0])cylinder(d=d,h=h,$fn=fn,center=center);}
    }//end for
}//end columns

module center_spokes(){
rotate([0,0,30])green_beams(w=200,h=15.5,l=15.5,r=15);
translate([0,0,7.5]){rotate([0,0,30])side_holes(n=0,buffer=0,r=150,l=250,w=3.5);}
translate([0,0,7.5]){rotate([0,0,30])side_holes(n=0,buffer=0,r=228,l=20,w=7.2,fn=6);}}

module main_assembly(){
difference(){
    //main shape
    translate([0,0,-5])green_beams(w=20,h=20,l=228.5,r=178);
    // cut out actual beams
    green_beams(w=15.5+1.5,h=15.5);
    //split into segments
    translate([0,0,-10])green_beams(w=60,h=60,l=70,r=175);
    //add nut slots for easier assembly
    translate([0,0,15-3.6])green_beams(w=5.7,h=3.7,l=250,r=185);
    translate([0,0,7.5])side_holes(w=3.5);
    
    center_spokes();
    
    rotate([0,0,15])columns(n=12,d=3.5,h=10,r=195);
    translate([0,0,-7])rotate([0,0,15])columns(n=12,d=7,h=10,r=195,center=true);
}//end difference
}
//main_assembly();

module single_hex_join(){
    intersection(){
        main_assembly();
        translate([100,150,0])cube(150,center=true);
    }
}//end single_hex_join

//single_hex_join();

module new_flybase(){
    difference(){
        translate([0,0,0])platform();
        translate([0,0,1.5]){rotate([0,0,30])columns(d=3.4,r=30,h=3,n=6,fn=30);}
    }//end difference
}//end new_flybase

translate([0,0,15])new_flybase();

module center_join(){
difference(){
translate([0,0,-4.5])cylinder(d=100,h=20,$fn=60);
center_spokes();
columns(d=3.4,h=30,r=40,n=6);
    translate([0,0,-17])columns(d=7.2,h=30,r=40,n=6);
    columns(d=3.4,h=30,r=25,n=6);
    translate([0,0,-17])columns(d=7.2,h=30,r=25,n=6);
    translate([0,0,14]){rotate([0,0,30])columns(d=3.4,r=30,h=3,n=6,fn=30);}
}}

//center_join();

module camera_joints(l=360,r=195.5,size=120){
    $fn=6;
    offset=5;
    difference(){
        translate([-7.5,l/2-size+30.5+offset,l-size+15+offset])cube([20,size,size]);
        translate([-7.5,l/2-size+30.5+offset,l]){rotate([-135,0,0])cube(size*2);}
        
        add_rounds(R=offset,fn=20,axis="x"){translate([0,0,0]){difference(){
            translate([-7.5,l/2-size+15-offset,l-size-offset]){cube([20,size,size]);}
        translate([-7.5,l/2-size+30.5+offset+size/10,l+size/10]){rotate([-135,0,0]){cube(size*2);}}}
        }}//end translate / rounds
        
        translate([0,r+15/2,l/2])cube([15,15,l],center=true);
        translate([0,-r-15/2,l/2])cube([15,15,l],center=true);
        translate([0,0,l+15/2])cube([15,l,15],center=true);
        
        //screw holes
        translate([0,r+15/2,l-10]){rotate([0,90,0])cylinder(d=3.5,h=40,center=true);}
        translate([0,r+15/2,l-size/2]){rotate([0,90,0])cylinder(d=3.5,h=40,center=true);}
        translate([size/2+7.5+2,r+15/2,l-10]){rotate([0,90,0])cylinder(d=6.6,h=size,center=true);}
        translate([size/2+7.5+2,r+15/2,l-size/2]){rotate([0,90,0])cylinder(d=6.6,h=size,center=true);}
        
        
        translate([0,r+15/2,l-30]){rotate([0,90,90])cylinder(d=3.5,h=40,center=true);}
        translate([0,r+17+size/2,l-30]){rotate([0,90,90])cylinder(d=6.6,h=size,center=true);}
        translate([0,r+15/2,l-size/2-20]){rotate([0,90,90])cylinder(d=3.5,h=size,center=true);}
        translate([0,r-2-size/2,l-size/2-20]){rotate([0,90,90])cylinder(d=6.6,h=size,center=true);}
        
        translate([0,r-25,l+7.5]){rotate([0,90,0])cylinder(d=3.5,h=40,center=true);}
        translate([0,r-size/2,l+7.5]){rotate([0,90,0])cylinder(d=3.5,h=40,center=true);}
        translate([size/2+7.5+2,r-25,l+7.5]){rotate([0,90,0])cylinder(d=6.6,h=size,center=true);}
        translate([size/2+7.5+2,r-size/2,l+7.5]){rotate([0,90,0])cylinder(d=6.6,h=size,center=true);}
        
        translate([0,r-40,l+7.5]){rotate([0,0,0])cylinder(d=3.5,h=40,center=true);}
        translate([0,r-40,l+17+size/2]){rotate([0,0,0])cylinder(d=6.6,h=size,center=true);}
        translate([0,r-size/2-20,l+7.5]){rotate([0,0,0])cylinder(d=3.5,h=size,center=true);}
        translate([0,r-size/2-20,l-2-size/2]){rotate([0,0,0])cylinder(d=6.6,h=size,center=true);}
        
    }//end difference
}//end camera_joints

//camera_joints();
