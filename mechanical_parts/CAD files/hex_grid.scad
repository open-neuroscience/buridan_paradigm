module hex_grid(x=50,y=60,z=5,d=5,thickness=2){
    $fn=6;
    //some important equations:
    side_l = d/2;
    short_width = (3^0.5)*side_l;
    nx = round(x/d/1.5);
    ny = round(y/short_width);
    color("orange",0.3)
    linear_extrude(z){
    translate([d/2,short_width/2])
    for(pos_x=[0:nx]){
        for(pos_y=[0:ny]){
            //echo(pos_x,pos_y);
            tx = pos_x*d*1.5;
            ty = pos_y*short_width;
            txa = tx+d*3/4;
            tya = ty+(short_width/2);
            if (tx+d<x && ty+short_width<y){
            color("red",0.5)translate([tx,ty])circle(d=d-thickness);}
            
            if (txa+d<x && tya+short_width<y){
                //echo("d=",d);
                //echo("d/2=",d/2);
                //echo("txa = ",txa);
                //echo(txa+d/2);
            translate([txa,tya])
            color("green",0.5)
                circle(d=d-thickness);}
    }}}
}//end hex_grid


hex_grid(d=10,x=100);


