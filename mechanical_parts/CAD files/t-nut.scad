use <fillets_and_rounds.scad>

difference(){
    add_rounds(fn=10,R=0.6)cube([5.3,15,2.3],center=true);
    cylinder(h=5,d=3.1,$fn=20,center=true);
    
}//end difference