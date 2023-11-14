//????¦Ã???????
int multiConicProjection(double *x,double *y,double B, double L, double midlL,double mapScale)//6371116
//B??L??midlL ?????
//mapScale :????????
//midlL:??????
{
	
	/*   ********************???????????****************  */

  
	
  	double A,l,p;
  	

  	double R,ln;
  	double x1,xn,yn;
  	double An;
  	double q;
  	 
  	
  	R = 6371116;;
  	ln = dgch(201.6);
 
  	l = L - midlL;

  	if(B<0)
  	{ 
  		B=B*(-1);

    	x1=(B+0.06683225*pow(B,4))*R/140000;
    	xn=x1+0.20984*B*R/140000;
    	yn=sqrt(pow(112,2)-pow(xn,2))+20;    

    	q=(pow(yn,2)+pow(xn-x1,2))/(2*(xn-x1));
    	An=asin(yn/q);
    	A=An*l/ln;

    	x[0]=(-1)*(x1+q*(1-cos(A)))*14000*0.888428/mapScale;   
    	//x[0]=(-1)*(x1+q*(1-cos(A)))*14000/mapScale;     
   		y[0]=q*sin(A)*14000/mapScale;
  	}
  	else if(B==0)
  	{ 
  		x1=(B+0.06683225*pow(B,4))*R/140000;
    	xn=x1+0.20984*B*R/140000;
    	yn=sqrt(pow(112,2)-pow(xn,2))+20;

    	x[0]=0;
    	y[0]=yn*l/ln*14000/mapScale;
  	}
  	else if(B>0)
  	{ 
  		x1=(B+0.06683225*pow(B,4))*R/140000;
    	xn=x1+0.20984*B*R/140000;
    	yn=sqrt(pow(112,2)-pow(xn,2))+20;

    	q=(pow(yn,2)+pow(xn-x1,2))/(2*(xn-x1));
    	An=asin(yn/q);
    	A=An*l/ln;

    	x[0]=(x1+q*(1-cos(A)))*14000*0.888428/mapScale;
    	//x[0]=(x1+q*(1-cos(A)))*14000/mapScale;
    	y[0]=q*sin(A)*14000/mapScale;
  	}
  	
	return 1;
}

