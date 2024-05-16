# -*- coding: cp936 -*-
import math
import sys

phi0 = math.radians(35) # ��׼γ��35��
lambda0 = math.radians(104) # ���뾭��104��
R = 6371000 # ����뾶���ɸ����Լ����赥λ��������
rotation = 0 # ��ת�ǣ��еĹ�ʽ����Ϊ15�ȣ����������ԣ��������ĿӦ����0


def calculate_Z_alpha(phi, lambda_): # ��ʽ��Ҳ����ԭ������Ӳд�ɴ����¡��������ǽ���γ��ת�������漫����
    phi = math.radians(phi)
    lambda_ = math.radians(lambda_)

    cosZ = math.sin(phi) * math.sin(phi0) + math.cos(phi) * math.cos(phi0) * math.cos(lambda_ - lambda0)
    Z = math.acos(cosZ)
    sinZcosAlpha = math.sin(phi) * math.cos(phi0) - math.cos(phi) * math.sin(phi0) * math.cos(lambda_ - lambda0)
    sinZsinAlpha = math.cos(phi) * math.sin(lambda_ - lambda0)

    alpha = math.atan2(sinZsinAlpha, sinZcosAlpha)

    return math.degrees(Z), math.degrees(alpha)


def calculate_x_y(Z, alpha): # �����������漫����ת����α��λͶӰ����
    alpha = math.radians(alpha)

    delta = alpha + 0.005308 * math.radians(Z) * math.sin(3 * (math.radians(rotation) + alpha)) / 0.453786

    x = math.radians(Z) * math.cos(delta) * R # ֻ���������õ�R����ֻ���г˷�����
    y = math.radians(Z) * math.sin(delta) * R

    return x, y


def transform_coords(lon, lat): # ������������ʽ����ͶӰ�任
    Z_alpha = calculate_Z_alpha(lat, lon)
    Z = Z_alpha[0]
    alpha = Z_alpha[1]
    x_y = calculate_x_y(Z, alpha)
    x = x_y[0]
    y = x_y[1]

    # point = arcpy.Point(x, y) # �ĳ���������ʽ��ע��x��y��λ��
    return y, x

def main():
    lon = 150.2221679687
    lat = 48.0000953674316
    yx = transform_coords(lon, lat)
    y = yx[0]
    x = yx[1]

    print("γ��Ϊ: " + str(lon) + " ����Ϊ: " + str(lat) + " ������Ϊ: " + str(y) + " ������Ϊ: " + str(x))

    
if __name__ == '__main__':
    main()

