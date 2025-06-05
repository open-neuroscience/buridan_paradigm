use <flybase.scad>

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
    translate([0,0,7.5])side_holes();
    
    center_spokes();
    
    rotate([0,0,15])columns(n=12,d=3.5,h=10,r=195);
    translate([0,0,-7])rotate([0,0,15])columns(n=12,d=7,h=10,r=195,center=true);
}//end difference
}
//main_assembly();

//translate([0,0,15])platform();

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

center_join();
