pub const TOLERANCE: f32 = 0.001;

pub fn approximately(value: f32, target: f32) -> bool {
    (value - target).abs() < TOLERANCE
}

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct Vec4 {
    pub x: f32,
    pub y: f32,
    pub z: f32,
    pub w: f32,
}

impl Vec4 {
    #[allow(dead_code)]
    pub const fn new(x: f32, y: f32, z: f32, w: f32) -> Self {
        Self { x, y, z, w }
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Mat4 {
    pub m11: f32,
    pub m12: f32,
    pub m13: f32,
    pub m14: f32,
    pub m21: f32,
    pub m22: f32,
    pub m23: f32,
    pub m24: f32,
    pub m31: f32,
    pub m32: f32,
    pub m33: f32,
    pub m34: f32,
    pub m41: f32,
    pub m42: f32,
    pub m43: f32,
    pub m44: f32,
}

impl Mat4 {
    pub const IDENTITY: Mat4 = Mat4 {
        m11: 1.0,
        m12: 0.0,
        m13: 0.0,
        m14: 0.0,
        m21: 0.0,
        m22: 1.0,
        m23: 0.0,
        m24: 0.0,
        m31: 0.0,
        m32: 0.0,
        m33: 1.0,
        m34: 0.0,
        m41: 0.0,
        m42: 0.0,
        m43: 0.0,
        m44: 1.0,
    };

    pub fn from_row_major(m: &[f32; 16]) -> Self {
        Self {
            m11: m[0],
            m12: m[1],
            m13: m[2],
            m14: m[3],
            m21: m[4],
            m22: m[5],
            m23: m[6],
            m24: m[7],
            m31: m[8],
            m32: m[9],
            m33: m[10],
            m34: m[11],
            m41: m[12],
            m42: m[13],
            m43: m[14],
            m44: m[15],
        }
    }

    pub fn from_rows3(row0: Vec4, row1: Vec4, row2: Vec4) -> Self {
        Self {
            m11: row0.x,
            m12: row0.y,
            m13: row0.z,
            m14: row0.w,
            m21: row1.x,
            m22: row1.y,
            m23: row1.z,
            m24: row1.w,
            m31: row2.x,
            m32: row2.y,
            m33: row2.z,
            m34: row2.w,
            m41: 0.0,
            m42: 0.0,
            m43: 0.0,
            m44: 1.0,
        }
    }

    fn row(&self, i: usize) -> [f32; 4] {
        match i {
            0 => [self.m11, self.m12, self.m13, self.m14],
            1 => [self.m21, self.m22, self.m23, self.m24],
            2 => [self.m31, self.m32, self.m33, self.m34],
            _ => [self.m41, self.m42, self.m43, self.m44],
        }
    }

    fn col(&self, j: usize) -> [f32; 4] {
        [
            self.row(0)[j],
            self.row(1)[j],
            self.row(2)[j],
            self.row(3)[j],
        ]
    }

    fn at(&self, i: usize, j: usize) -> f32 {
        self.row(i)[j]
    }

    fn from_rows(rows: [[f32; 4]; 4]) -> Self {
        Self {
            m11: rows[0][0],
            m12: rows[0][1],
            m13: rows[0][2],
            m14: rows[0][3],
            m21: rows[1][0],
            m22: rows[1][1],
            m23: rows[1][2],
            m24: rows[1][3],
            m31: rows[2][0],
            m32: rows[2][1],
            m33: rows[2][2],
            m34: rows[2][3],
            m41: rows[3][0],
            m42: rows[3][1],
            m43: rows[3][2],
            m44: rows[3][3],
        }
    }

    #[must_use]
    #[allow(clippy::needless_range_loop)]
    pub fn mul(&self, rhs: &Mat4) -> Mat4 {
        let mut rows = [[0.0f32; 4]; 4];
        for i in 0..4 {
            for j in 0..4 {
                let mut sum = 0.0;
                for k in 0..4 {
                    sum += self.at(i, k) * rhs.col(j)[k];
                }
                rows[i][j] = sum;
            }
        }
        Mat4::from_rows(rows)
    }

    #[must_use]
    pub fn transform(&self, v: Vec4) -> Vec4 {
        let dot = |r: [f32; 4]| r[0] * v.x + r[1] * v.y + r[2] * v.z + r[3] * v.w;
        Vec4::new(
            dot(self.row(0)),
            dot(self.row(1)),
            dot(self.row(2)),
            dot(self.row(3)),
        )
    }

    #[must_use]
    pub fn transpose(&self) -> Mat4 {
        Mat4::from_rows([self.col(0), self.col(1), self.col(2), self.col(3)])
    }

    #[must_use]
    #[allow(clippy::needless_range_loop)]
    pub fn invert(&self) -> Option<Mat4> {
        let m = [
            [self.m11, self.m12, self.m13, self.m14],
            [self.m21, self.m22, self.m23, self.m24],
            [self.m31, self.m32, self.m33, self.m34],
            [self.m41, self.m42, self.m43, self.m44],
        ];

        let minor = |r: usize, c: usize| -> f32 {
            let mut vals = [0.0f32; 9];
            let mut idx = 0;
            for i in 0..4 {
                if i == r {
                    continue;
                }
                for j in 0..4 {
                    if j == c {
                        continue;
                    }
                    vals[idx] = m[i][j];
                    idx += 1;
                }
            }
            vals[0] * (vals[4] * vals[8] - vals[5] * vals[7])
                - vals[1] * (vals[3] * vals[8] - vals[5] * vals[6])
                + vals[2] * (vals[3] * vals[7] - vals[4] * vals[6])
        };

        let mut cofactors = [[0.0f32; 4]; 4];
        for i in 0..4 {
            for j in 0..4 {
                let sign = if (i + j) % 2 == 0 { 1.0 } else { -1.0 };
                cofactors[i][j] = sign * minor(i, j);
            }
        }

        let det = m[0][0] * cofactors[0][0]
            + m[0][1] * cofactors[0][1]
            + m[0][2] * cofactors[0][2]
            + m[0][3] * cofactors[0][3];

        if det.abs() < f32::EPSILON {
            return None;
        }
        let inv_det = 1.0 / det;

        let mut rows = [[0.0f32; 4]; 4];
        for i in 0..4 {
            for j in 0..4 {
                rows[i][j] = cofactors[j][i] * inv_det;
            }
        }
        Some(Mat4::from_rows(rows))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn approx_eq(a: &Mat4, b: &Mat4) {
        let a_rows = [a.row(0), a.row(1), a.row(2), a.row(3)];
        let b_rows = [b.row(0), b.row(1), b.row(2), b.row(3)];
        for i in 0..4 {
            for j in 0..4 {
                assert!(
                    (a_rows[i][j] - b_rows[i][j]).abs() < 1e-4,
                    "mismatch at ({i},{j}): {} vs {}",
                    a_rows[i][j],
                    b_rows[i][j]
                );
            }
        }
    }

    #[test]
    fn identity_times_identity_is_identity() {
        approx_eq(&Mat4::IDENTITY.mul(&Mat4::IDENTITY), &Mat4::IDENTITY);
    }

    #[test]
    fn identity_is_its_own_inverse() {
        approx_eq(&Mat4::IDENTITY.invert().unwrap(), &Mat4::IDENTITY);
    }

    #[test]
    fn identity_is_its_own_transpose() {
        approx_eq(&Mat4::IDENTITY.transpose(), &Mat4::IDENTITY);
    }

    #[test]
    fn matrix_times_inverse_is_identity() {
        let m = Mat4::from_row_major(&[
            2.0, 0.0, 0.0, 0.0, //
            0.0, 3.0, 0.0, 0.0, //
            0.0, 0.0, 4.0, 0.0, //
            5.0, 6.0, 7.0, 1.0,
        ]);
        let inv = m.invert().expect("matrix should be invertible");
        approx_eq(&m.mul(&inv), &Mat4::IDENTITY);
    }

    #[test]
    fn singular_matrix_has_no_inverse() {
        let singular = Mat4::from_row_major(&[0.0; 16]);
        assert!(singular.invert().is_none());
    }

    #[test]
    fn transpose_swaps_rows_and_columns() {
        let m = Mat4::from_row_major(&[
            1.0, 2.0, 3.0, 4.0, //
            5.0, 6.0, 7.0, 8.0, //
            9.0, 10.0, 11.0, 12.0, //
            13.0, 14.0, 15.0, 16.0,
        ]);
        let t = m.transpose();
        assert_eq!(t.m12, m.m21);
        assert_eq!(t.m41, m.m14);
        assert_eq!(t.m34, m.m43);
    }

    #[test]
    fn from_rows3_fills_affine_bottom_row() {
        let m = Mat4::from_rows3(
            Vec4::new(1.0, 0.0, 0.0, 0.0),
            Vec4::new(0.0, 1.0, 0.0, 0.0),
            Vec4::new(0.0, 0.0, 1.0, 0.0),
        );
        approx_eq(&m, &Mat4::IDENTITY);
    }
}
