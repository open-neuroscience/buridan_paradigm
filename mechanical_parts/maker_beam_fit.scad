//makerbeam replica only keeping the bits that are interesting to us:

//units are mm

//makerbeamXL:

wid = 15;
leng = 15;
offs = 4.8;
tol = 0.12;
horiz = 5.5;
thick_hor = 2.3;
vert = 5;
thick_ver = 3;

cube([wid,leng,10],center=true);
for(i = [0 : 1 : 3]){
rotate([0,0,i*90]){
translate([0,wid/2-thick_hor/2-1,9]){
cube([horiz-2*tol,thick_hor-2*tol,10],center=true);
}//end translate
translate([0,wid/2-thick_hor/2-1.35,9]){
cube([thick_ver-2*tol,vert-2*tol,10],center=true);
}//end translate
}//end rotate
}