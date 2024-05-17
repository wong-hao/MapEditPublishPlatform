# -*- coding: UTF -8 -*-
import math
import sys

phi0 = math.radians(35) # 基准纬线35度
lambda0 = math.radians(104) # 中央经线104度
R = 6371000 # 地球半径（可根据自己所需单位调整））
rotation = 0 # 旋转角，有的公式设置为15度，但经过测试，在这个项目应该是0

def calculate_Z_alpha(phi, lambda_): # 公式我也不懂原理，就硬写成代码呗。这里大概是将经纬度转换成球面极坐标
    phi = math.radians(phi)
    lambda_ = math.radians(lambda_)

    cosZ = math.sin(phi) * math.sin(phi0) + math.cos(phi) * math.cos(phi0) * math.cos(lambda_ - lambda0)
    Z = math.acos(cosZ)
    sinZcosAlpha = math.sin(phi) * math.cos(phi0) - math.cos(phi) * math.sin(phi0) * math.cos(lambda_ - lambda0)
    sinZsinAlpha = math.cos(phi) * math.sin(lambda_ - lambda0)

    alpha = math.atan2(sinZsinAlpha, sinZcosAlpha)

    return math.degrees(Z), math.degrees(alpha)


def calculate_x_y(Z, alpha): # 这里大概是球面极坐标转换成伪方位投影坐标
    alpha = math.radians(alpha)

    delta = alpha + 0.005308 * math.radians(Z) * math.sin(3 * (math.radians(rotation) + alpha)) / 0.453786

    x = math.radians(Z) * math.cos(delta) * R # 只有这两行用到R，且只进行乘法运算
    y = math.radians(Z) * math.sin(delta) * R

    return x, y


def transform_coords(lon, lat): # 用上面两个公式进行投影变换
    Z_alpha = calculate_Z_alpha(lat, lon)
    Z = Z_alpha[0]
    alpha = Z_alpha[1]
    x_y = calculate_x_y(Z, alpha)
    x = x_y[0]
    y = x_y[1]

    # point = arcpy.Point(x, y) # 改成你的输出格式，注意x和y的位置
    return x, y

def main():
    lon = 111.64958190918
    lat = 36.0427093505859
    yx = transform_coords(lon, lat)
    x = yx[0]
    y = yx[1]

    print("longitude: " + str(lon) + " latitude: " + str(lat) + " xCoordination: " + str(x) + " yCoordination: " + str(y))

    
if __name__ == '__main__':
    main()

